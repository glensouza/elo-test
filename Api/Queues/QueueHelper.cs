using System;
using System.Text;

namespace Api.Queues;

public class QueueHelper
{
    public static string PrepareMessageString(string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        string notification = Convert.ToBase64String(bytes);
        return notification;
    }
}
