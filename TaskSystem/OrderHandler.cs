using System;
using System.Linq;
using UnityEngine;
using Packages.com.ianritter.unityscriptingtools.Runtime.Services.CustomLogger;
using SpaceSim.Scripts.Runtime.AI.TaskSystem.EventChannels;
using SpaceSim.Scripts.Runtime.Entities.Wares;
using static Packages.com.ianritter.unityscriptingtools.Runtime.Services.TextFormatting.TextFormat;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem
{
    public class OrderHandler
    {
        private readonly OrderManagerChannel _orderManagerChannel;
        private readonly Order[] _orders;
        private readonly string _homeFactoryName;
        private readonly CustomLogger _logger;
        private int _orderNumber;


        public OrderHandler( OrderManagerChannel orderManagerChannel, WareDepot homeFactory, int totalOrders, int scansBeforeCancel, 
            CustomLogger logger, CustomLogger orderLogger )
        {
            _orderManagerChannel = orderManagerChannel;
            // _orderManagerChannel.OnOrderComplete += OnOrderComplete;
            _homeFactoryName = homeFactory.GetDisplayName();
            _logger = logger;
            
            _orders = new Order[totalOrders];
            for (int i = 0; i < totalOrders; i++)
            {
                _orders[i] = new Order( i, homeFactory, scansBeforeCancel, orderLogger );
                _orders[i].GetOrderDetailsChannel().OnOrderComplete += OnOrderComplete;
                _orders[i].GetOrderDetailsChannel().OnOrderCanceled += OnOrderCanceled;
            }
        }

        /// <summary> Returns true if an order slot is available. </summary>
        public bool OrderIsAvailable() => _orders.Any( order => order.IsFree() );
        
        /// <summary> Returns true if an order exists that has been created but not yet assigned. </summary>
        public bool HasOpenOrder()
        {
            foreach ( Order order in _orders )
            {
                if ( !order.IsOpen() ) continue;
                
                _logger.Log( $"Order {GetColoredStringYellow( order.GetOderId().ToString() )} is open, returning true." );
                _logger.Log( $"    Order {GetColoredStringYellow( order.GetOderId().ToString() )}'s name is {GetColoredStringYellowGreen( order.GetOrderName() )}." );
                    
                return true;
            }
            _logger.Log( "No open orders were found. Returning false." );
            return false;
        }
        
        /// <summary> Returns true if an order exists that has been created but not yet assigned. </summary>
        public bool HasOpenNoOrders()
        {
            foreach ( Order order in _orders )
            {
                if ( !order.IsOpen() ) continue;
                
                _logger.Log( $"Order {GetColoredStringYellow( order.GetOderId().ToString() )} is open, returning false." );
                _logger.Log( $"    Order {GetColoredStringYellow( order.GetOderId().ToString() )}'s name is {GetColoredStringYellowGreen( order.GetOrderName() )}." );
                    
                return false;
            }
            _logger.Log( "No open orders were found. Returning true." );
            return true;
        }

        /// <summary>
        /// Returns the first order in the collection that is open and awaiting assignment. Returns null if none are available.
        /// Use HasOpenOrder to check if an order exists that has not yet been assigned.
        /// </summary>
        public Order GetOpenOrder() => _orders.FirstOrDefault( order => order.IsOpen() );

        public int GetOrderCount() => _orders.Length;

        public int GetOrdersInUseCount() => _orders.Count( order => order.IsOpen() );

        public Order GetOrder( int index )
        {
            if ( index > (_orders.Length - 1) )
                throw new ArgumentOutOfRangeException();
            
            return _orders[index];
        }
        
        private Order GetFreeOrder() => _orders.FirstOrDefault( order => order.IsFree() );

        /// <summary>
        /// Create an Order object for the specified wareType. Returns true if successful.
        /// Use OrderIsAvailable before calling this method to see if an Order object is available.
        /// </summary>
        public Order PlaceOrder( Ware ware, int wareIndex, int quantity )
        {
            Order order = GetFreeOrder();
            if ( order == null )
            {
                Debug.LogWarning( $"OrderHandler: Order for {GetColoredStringYellow(quantity.ToString())} " +
                                  $"{GetColoredStringGreen(ware.GetTypeName())}(s) can't be placed as no Order objects are available." );
                return null;
            }
            
            IncrementOrderNumber();
            
            order.PopulateOrder( GetOrderName( ware, quantity ), _orderNumber, ware, wareIndex, quantity );
            
            // Attach the order to a order details panel in the orders panel, then broadcast order details to update the panel.
            order.BroadcastUiInitialization();
            
            // Broadcast that an open order is now ready for worker assignment.
            _orderManagerChannel.OnOpenOrderReadyNotify();
            
            // If this used that last open order, broadcast that all free orders are now taken.
            if ( !OrderIsAvailable() )
                _orderManagerChannel.OnAllFreeOrdersTakenNotify();
            
            _orderManagerChannel.OnActiveOrderUpdatedNotify( order.GetOderId(), true );
            
            return order;
        }
        
        private void IncrementOrderNumber()
        {
            _orderNumber++;
            if ( _orderNumber > 999 )
                _orderNumber = 1;
        }

        private void OnOrderCanceled( Order order )
        {
            // Pass the order canceled signal back up to the order manager.
            _orderManagerChannel.OnOrderCanceledNotify( order );
            
            ResetOrder( order );
        }

        private void OnOrderComplete( Order order )
        {
            // Pass the order complete signal back up to the order manager.
            _orderManagerChannel.OnOrderCompleteNotify( order );
            ResetOrder( order );
        }

        private void ResetOrder( Order order )
        {
            _orderManagerChannel.OnActiveOrderUpdatedNotify( order.GetOderId(), false );

            _orderManagerChannel.OnFreeOrderAvailableNotify();
            
            if ( !HasOpenOrder() )
                _orderManagerChannel.OnAllOrdersAssignedNotify();
        }

        private string GetOrderName( Ware ware, int quantity )
        {
            return $"{_homeFactoryName}, {quantity.ToString()} x {ware.GetTypeName()}(s)";
        }
    }
}
