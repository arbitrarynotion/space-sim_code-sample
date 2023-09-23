using System;
using SpaceSim.Scripts.Runtime.Entities.Wares;
using SpaceSim.Scripts.Runtime.Entities.Workers;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem.EventChannels
{
    public class OrderChannel
    {
        public event Action<int, string, string> OnProductsOrderedUpdated;
        public void OnProductsOrderedUpdatedNotify( int orderQuantity, string wareCode, string wareTypeName ) => OnProductsOrderedUpdated?.Invoke( orderQuantity, wareCode, wareTypeName );
        
        public event Action<Order, WareDepot, Worker> OnActorsAssigned;
        public void OnActorsAssignedNotify( Order order, WareDepot wareDepot, Worker worker ) => OnActorsAssigned?.Invoke( order, wareDepot, worker );
        
        public event Action<int> OnAssignmentTimerUpdated;
        public void OnAssignmentTimerUpdatedNotify( int count ) => OnAssignmentTimerUpdated?.Invoke( count );
        
        public event Action<string> OnAssignmentWaitingUpdated;
        public void OnAssignmentWaitingUpdatedNotify( string waitingText ) => OnAssignmentWaitingUpdated?.Invoke( waitingText );
        
        public event Action<string> OnAssignmentStatusUpdated;
        public void OnAssignmentStatusUpdatedNotify( string text ) => OnAssignmentStatusUpdated?.Invoke( text );
        
        public event Action<int> OnFailedScansCountUpdated;
        public void OnFailedScansCountUpdatedNotify( int count ) => OnFailedScansCountUpdated?.Invoke( count );
        
        public event Action<string> OnPickupPointSet;
        public void OnPickupPointSetNotify( string pickupPointName ) => OnPickupPointSet?.Invoke( pickupPointName );
        
        public event Action<string> OnWorkerStatusUpdated;
        public void OnWorkerStatusUpdatedNotify( string text ) => OnWorkerStatusUpdated?.Invoke( text );
        
        public event Action<Order, int> OnWareCollected;
        public void OnWareCollectedNotify( Order order, int amount ) => OnWareCollected?.Invoke( order, amount );
        
        public event Action<Order, int> OnWareDelivered;
        public void OnWareDeliveredNotify( Order order, int amount ) => OnWareDelivered?.Invoke( order, amount );
        
        public event Action<Order> OnWareTransferComplete;
        public void OnWareTransferCompleteNotify( Order order ) => OnWareTransferComplete?.Invoke( order );
        
        public event Action<int, int> OnOderQuantityUpdated;
        public void OnOderQuantityUpdatedNotify( int wareIndex, int amountRemoved ) => OnOderQuantityUpdated?.Invoke( wareIndex, amountRemoved );
        
        public event Action<Order> OnOrderComplete;
        public void OnOrderCompleteNotify( Order order ) => OnOrderComplete?.Invoke( order );
        
        public event Action<Order> OnOrderCanceled;
        public void OnOrderCanceledNotify( Order order ) => OnOrderCanceled?.Invoke( order );
    }
}