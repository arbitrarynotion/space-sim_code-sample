using System;
using UnityEngine;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem.EventChannels
{
    public class OrderAssignmentChannel : MonoBehaviour
    {
        public event Action OnScanComplete;
        public void OnScanCompleteNotify() => OnScanComplete?.Invoke();
        
        public event Action<string> OnOrderAssignmentManagerStatusUpdated;
        public void OnOrderAssignmentManagerStatusUpdatedNotify( string status ) => OnOrderAssignmentManagerStatusUpdated?.Invoke( status );
    }
}