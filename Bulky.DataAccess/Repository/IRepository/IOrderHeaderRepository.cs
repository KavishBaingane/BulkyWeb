using BulkyBook.Models;
using BulkyBook.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.DataAccess.Repository.IRepository
{
    public interface IOrderHeaderRepository : IRepository<OrderHeader>
    {
        void Update(OrderHeader obj);
        void UpdateStatus(int id , string OrderStatus, string? PaymentStatus = null);
        void UpdateStripePaymentId(int id, string sessionId, string PaymentIntentId);
        
    }
}
