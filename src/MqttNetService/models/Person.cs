namespace MqttNetService.Models;
public class Person
{
    public String Topic {get; set;}
    public Person(String t)
    {
        Topic = t;
    }
}