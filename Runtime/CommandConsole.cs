using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public class CommandConsole
{
    static readonly Dictionary<string, List<IGenericDelegate>> s_delegados = new();

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void SearchMethods()
    {
        var allClasses = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => t.IsClass));

        foreach (var @class in allClasses)
        {
            foreach (var method in @class.GetMethods())
            {
                if (!method.CustomAttributes.Any(a => a.AttributeType == typeof(CommandAttribute))) 
                    continue;

                var assemblyName = method.DeclaringType.Assembly.GetName().Name;

                var key =
                    assemblyName + '.'
                    + method.DeclaringType.Name + '.'
                    + method.Name;

                IGenericDelegate genericDelegate;
                
                if (method.IsStatic)
                {
                    genericDelegate = new GenericStaticDelegate(method);
                }
                else
                {
                    var getter = @class.GetMethods().FirstOrDefault(m => m.CustomAttributes.Any(a => a.AttributeType == typeof(InstanceCommandGetterAttribute)));

                    if (getter == null)
                    {
                        //Debug.LogWarning($"La clase {@class.Name} no ")
                    }

                    genericDelegate = new GenericInstanceDelegate(method, GetObjects);

                    IEnumerable<object> GetObjects()
                    {
                        if(getter==null)
                            yield break;
                        
                        if (getter.ReturnType.IsAssignableFrom(typeof(IEnumerable<object>)))
                        {
                            foreach (var instance in getter.Invoke(null, null) as IEnumerable<object>)
                            {
                                yield return instance;
                            }
                            yield break;
                        }

                        yield return getter.Invoke(null, null);
                    }
                }

                AddCommand(key, genericDelegate, out var list);
                
                CommandAttribute attribute = (CommandAttribute)Attribute.GetCustomAttribute(method, typeof(CommandAttribute));

                list[^1].Descriptor = attribute.descriptor;
            }
        }
    }

    static void AddCommand(string name, IGenericDelegate commandDelegate, out List<IGenericDelegate> list)
    {
        if (!s_delegados.TryGetValue(name, out list))
        {
            list = new List<IGenericDelegate>();
            s_delegados.Add(name, list);
        }
        
        if(!list.Contains(commandDelegate))
            list.Add(commandDelegate);
    }
    
    public static void AddCommand(string name, IGenericDelegate commandDelegate)
    {
        AddCommand(name, commandDelegate, out _);
    }

    public static void RemoveCommand(string name)
    {
        s_delegados.Remove(name);
    }
    
    public static void RemoveCommand(string name, IGenericDelegate commandDelegate)
    {
        if (!s_delegados.TryGetValue(name, out var list)) 
            return;
        
        list.Remove(commandDelegate);
            
        if (list.Count == 0)
            s_delegados.Remove(name);
    }

    public static void TriggerCommand(string command)
    {
        var parameters = command.Split(" ");

        if (!s_delegados.TryGetValue(parameters[0], out var delegates))
        {
            Debug.Log("No se encontro el metodo");
            return;
        }

        List<object> parametersConverted = new List<object>();

        foreach (var @delegate in delegates)
        {
            bool succes = true;

            parametersConverted.Clear();

            if (@delegate.MethodInfo.GetParameters().Length != parameters.Length - 1)
            {
                continue;
            }

            foreach (var paramtersType in @delegate.MethodInfo.GetParameters())
            {
                if (paramtersType.ParameterType == typeof(string))
                {
                    parametersConverted.Add(parameters[parametersConverted.Count + 1]);
                }
                else if (paramtersType.ParameterType == typeof(int))
                {
                    parametersConverted.Add(int.Parse(parameters[parametersConverted.Count + 1]));
                }
                else if (paramtersType.ParameterType == typeof(float))
                {
                    parametersConverted.Add(float.Parse(parameters[parametersConverted.Count + 1]));
                }
                else if (paramtersType.ParameterType == typeof(bool))
                {
                    parametersConverted.Add(bool.Parse(parameters[parametersConverted.Count + 1]));
                }
                else if (paramtersType.ParameterType == typeof(Vector2))
                {
                    var aux = parameters[parametersConverted.Count + 1].Split('-');

                    parametersConverted.Add(new Vector2(float.Parse(aux[0]), float.Parse(aux[1])));
                }
                else if (paramtersType.ParameterType == typeof(Vector3))
                {
                    var aux = parameters[parametersConverted.Count + 1].Split('-');

                    parametersConverted.Add(new Vector3(float.Parse(aux[0]), float.Parse(aux[1]), float.Parse(aux[3])));
                }
                else
                {
                    succes = false;
                    //Debug.LogError($"parameter not converted - Wanted:{paramtersType.Name} / Recibed: {parameters[parametersConverted.Count + 1]}");
                    break;
                }
            }

            if (succes)
            {
                @delegate.Trigger(parametersConverted.ToArray());
                return;
            }
        }
    }

    #region UI ONGUI
    static string _previousInput = "";
    static int _currentIndex = 0;
    static List<string> _predictions = GetPredictions(string.Empty).ToList();
    static GUIStyle _style;
    
    public static void OnGUI()
    {
        var e = Event.current;
        bool returnPressed = e.type == EventType.Used && e.keyCode == KeyCode.Return;
        bool tabPressed = e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab;
        bool upArrowPressed = e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow;
        bool downArrowPressed = e.type == EventType.KeyDown && e.keyCode == KeyCode.DownArrow;
        int i = 0;
        Color defaultColor = GUI.color;

        _style ??= new GUIStyle(GUI.skin.label) { richText = true };
        
        GUI.SetNextControlName("CommandInputField");
        
        string currentInput = GUILayout.TextField(_previousInput);
        
        if (currentInput != _previousInput)
        {
            _predictions.Clear();
            _predictions.AddRange(GetPredictions(currentInput));
        }
        
        if (GUILayout.Button("Enter") || returnPressed)
        {
            TriggerCommand(_previousInput.Trim());
        }

        if (upArrowPressed)
        {
            _currentIndex--;
        }
        else if (downArrowPressed)
        {
            _currentIndex++;
        }
        else if (e.type == EventType.KeyUp)
        {
            GUI.FocusControl("CommandInputField");
        }
        
        _currentIndex = (int)Mathf.Repeat(_currentIndex, _predictions.Count);

        if (tabPressed)
        {
            int index = _predictions[_currentIndex].IndexOf(' ');
        
            string command = index>=0 ? _predictions[_currentIndex].Substring(0,index) : _predictions[_currentIndex];
            
            currentInput = command;
            
            /*
            TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.GetControlID(FocusType.Keyboard));

            editor.cursorIndex = _currentInput.Length + 1;
            */
        }

        foreach (var commandName in _predictions)
        {
            GUI.color = defaultColor;
            if (i == _currentIndex)
            {
                GUI.color = Color.red;
            }
            GUILayout.Label(commandName, _style);
            i++;
        }
        
        _previousInput = currentInput;
    }
    
    static IEnumerable<string> GetPredictions(string input)
    {
        StringBuilder sb = new StringBuilder();

        input = input.TrimStart();

        string command;
        
        int index = input.IndexOf(' ');
        
        command = index>=0 ? input.Substring(0,index) : input;

        command = command.ToLower();

        IEnumerable<KeyValuePair<string, List<IGenericDelegate>>> lazzy;
        
        if(string.IsNullOrEmpty(command))
            lazzy = s_delegados.OrderBy(s => s.Key);
        else if(index<0)
            lazzy = s_delegados
                .Where(s => s.Key.ToLower().Contains(command))
                .OrderBy(s => s.Key);
        else
            lazzy = s_delegados
                .Where(s => s.Key.ToLower().Equals(command))
                .OrderBy(s => s.Key);
        
        foreach (var pair in lazzy)
        {
            sb.Clear();
            
            if (pair.Value.Count == 1)
            {
                sb.Append(pair.Key);
            }
            else
            {
                sb.Append($"{pair.Key} \tOverloads: {pair.Value.Count}");
            }

            if (index < 0)
            {
                yield return sb.ToString();
                continue;
            }
            
            foreach (var genericDelegate in pair.Value)
            {
                var parametersInfo = genericDelegate.MethodInfo.GetParameters();
                
                sb.Append($"\n\tstatus: {(genericDelegate.MethodInfo.IsStatic ? "Static" : genericDelegate.isSeted ? "Instanced" : "NotInstanced")}");

                sb.Append($"\tParams: ");
                
                if (parametersInfo.Length != 0)
                {
                    foreach (var parameterInfo in parametersInfo)
                    {
                        sb.Append('\t' + parameterInfo.ParameterType.Name);
                    }    
                }
                else
                {
                    sb.Append("\tNothing");
                }
                
                if (genericDelegate.Descriptor.Length != 0)
                {
                    sb.Append($"\n\t\tDescription: {genericDelegate.Descriptor}"); 
                }
            }
            
            yield return sb.ToString();
        }
    }

    #endregion
}

#region GENERIC DELEGATE

public interface IGenericDelegate
{
    bool isSeted { get; }
    
    MethodInfo MethodInfo { get; }
    
    string Descriptor { get; set; }
    
    void Trigger(params object[] param);
}

public class GenericEvent<TDelegate> : IGenericDelegate where TDelegate : System.Delegate
{
    public bool isSeted => @delegate != null;
    public MethodInfo MethodInfo => @delegate?.Method;
    public string Descriptor { get; set; } = string.Empty;

    public TDelegate @delegate;
    
    public void Trigger(params object[] param)
    {
        @delegate?.DynamicInvoke(param);
    }
}

public abstract class GenericDelegateParent : IGenericDelegate
{
    public abstract bool isSeted { get; }
    public MethodInfo MethodInfo { get;}

    public string Descriptor
    {
        get => _descriptor;
        set
        {
            if (value != null)
                _descriptor = value;
        } 
    }

    private string _descriptor = string.Empty;

    public GenericDelegateParent(MethodInfo methodInfo)
    {
        MethodInfo = methodInfo;
    }

    public abstract void Trigger(params object[] param);
}

public class GenericInstanceDelegate : GenericDelegateParent
{
    public Func<IEnumerable<object>> GetInstances;
    
    public override bool isSeted => GetInstances?.Invoke().FirstOrDefault() != null;

    public GenericInstanceDelegate(MethodInfo methodInfo, Func<IEnumerable<object>> getInstance) : base(methodInfo)
    {
        GetInstances = getInstance;
    }
    
    public override void Trigger(params object[] param)
    {
        if (!isSeted)
        {
            Debug.LogWarning($"No hay una instancia seleccionada del tipo <{MethodInfo.DeclaringType}>");
            return;
        }

        foreach (var instance in GetInstances.Invoke())
        {
            MethodInfo.Invoke(instance, param);
        }
    }
}

public class GenericStaticDelegate : GenericDelegateParent
{
    public override bool isSeted => true;
    
    public GenericStaticDelegate(MethodInfo methodInfo) : base(methodInfo)
    { }

    public override void Trigger(params object[] param)
    {
        MethodInfo.Invoke(null, param);
    }
}
#endregion

#region ATTRIBUTES

[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
public class CommandAttribute : System.Attribute
{
    public readonly string descriptor; 
    
    public CommandAttribute()
    {
    }
    
    public CommandAttribute(string descriptor)
    {
        this.descriptor = descriptor;
    }
}

public class InstanceCommandGetterAttribute : System.Attribute
{ }
#endregion
