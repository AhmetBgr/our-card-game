using System.Collections.Generic;
using UnityEngine.Events;

public class TrackableUnityEvent : UnityEvent
{
    private List<UnityAction> _listeners = new List<UnityAction>();

    public new void AddListener(UnityAction call)
    {
        _listeners.Add(call);
        base.AddListener(call);
    }

    public new void RemoveListener(UnityAction call)
    {
        _listeners.Remove(call);
        base.RemoveListener(call);
    }

    public IEnumerable<UnityAction> GetListeners()
    {
        return _listeners;
    }
}
