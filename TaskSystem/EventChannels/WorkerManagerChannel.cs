using System;
using UnityEngine;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem.EventChannels
{
    public class WorkerManagerChannel : MonoBehaviour
    {
        public event Action<string> OnWorkerManagerStatusUpdated;
        public event Action<int> OnIdleShipCountUpdated;

        public void OnWorkerManagerStatusUpdatedNotify( string status ) => OnWorkerManagerStatusUpdated?.Invoke( status );
        public void OnIdleShipCountUpdatedNotify( int count ) => OnIdleShipCountUpdated?.Invoke( count );

    }
}
