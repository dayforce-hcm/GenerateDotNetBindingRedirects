using Newtonsoft.Json;

public class Class1
{
    public string Run() => JsonConvert.SerializeObject(42);
}
