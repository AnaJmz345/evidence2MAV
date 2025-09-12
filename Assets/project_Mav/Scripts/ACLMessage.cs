using UnityEngine;

public class ACLMessage
{
    public DroneController Sender;
    public DroneMaster Receiver;
    public string Performative; // e.g. "Inform", "Request", "Permit"
    public string Content;      // e.g. "Found", "Land?", "StopSearch"

    public ACLMessage(DroneController from, DroneMaster to, string perf, string content)
    {
        Sender = from;
        Receiver = to;
        Performative = perf;
        Content = content;
    }

    public ACLMessage(string from, DroneController to, string perf, string content)
    {
        Sender = to;
        Performative = perf;
        Content = content;
    }
}