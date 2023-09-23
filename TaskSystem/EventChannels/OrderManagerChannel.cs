using System;
using UnityEngine;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem.EventChannels
{
    /// <summary>
    ///     This event channel is specific to the product or resource order manager it's associated with.
    ///     An order must communicate with both for both ware depots involved to stay up to date.
    /// </summary>
    public class OrderManagerChannel : MonoBehaviour
    {
        public event Action<string> OnOrderManagerStatusUpdated;
        public void OnOrderManagerStatusUpdatedNotify( string status ) => OnOrderManagerStatusUpdated?.Invoke( status );
        
        public event Action<int, bool> OnActiveOrderUpdated;
        public void OnActiveOrderUpdatedNotify( int orderIndex, bool isActive ) => OnActiveOrderUpdated?.Invoke( orderIndex, isActive );
        
        public event Action<Order> OnOrderCanceled;
        public void OnOrderCanceledNotify( Order order ) => OnOrderCanceled?.Invoke( order );

        public event Action<Order> OnOrderComplete;
        public void OnOrderCompleteNotify( Order order ) => OnOrderComplete?.Invoke( order );
        
        public event Action OnFreeOrderAvailable;
        public void OnFreeOrderAvailableNotify() => OnFreeOrderAvailable?.Invoke();
        
        public event Action OnAllFreeOrdersTaken;
        public void OnAllFreeOrdersTakenNotify() => OnAllFreeOrdersTaken?.Invoke();
        
        public event Action OnAllOpenOrdersTaken;
        public void OnAllOrdersAssignedNotify() => OnAllOpenOrdersTaken?.Invoke();
        
        public event Action OnOpenOrderReady;
        public void OnOpenOrderReadyNotify() => OnOpenOrderReady?.Invoke();
    }
}
