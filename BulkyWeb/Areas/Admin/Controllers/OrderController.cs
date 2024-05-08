using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Stripe.Climate;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Details(int orderId)
        {
            OrderViewModel
                 model= new OrderViewModel();
            model.OrderHeader = _unitOfWork.OrderHeader.Get(x => x.Id == orderId, includeProperties : "ApplicationUser");
            model.OrderDetails = _unitOfWork.OrderDetail.GetAll(x => x.OrderHeaderId == orderId, includeproperties: "Product");

            return View(model);
        }
        [HttpPost]
        //[Authorize(Roles =SD.Role_Admin+" "+SD.Role_Employee)] not working  not working
        public IActionResult UpdateOrderDetail(OrderViewModel model)
        {
           
            var orderHeaderFromDb = _unitOfWork.OrderHeader.Get(x => x.Id == model.OrderHeader.Id);
            orderHeaderFromDb.Name = model.OrderHeader.Name;
            orderHeaderFromDb.PhoneNumber = model.OrderHeader.PhoneNumber;
            orderHeaderFromDb.StreetAddress = model.OrderHeader.StreetAddress;
            orderHeaderFromDb.City = model.OrderHeader.City;
            orderHeaderFromDb.State = model.OrderHeader.State;
            orderHeaderFromDb.PoastalCode = model.OrderHeader.PoastalCode;
            if (!String.IsNullOrEmpty(model.OrderHeader.Carrier)){
                orderHeaderFromDb.Carrier = model.OrderHeader.Carrier;
            }
            if (!String.IsNullOrEmpty(model.OrderHeader.TrackingNumber)){
                orderHeaderFromDb.Carrier = model.OrderHeader.TrackingNumber;
            }
            _unitOfWork.OrderHeader.Update(orderHeaderFromDb);
            _unitOfWork.Save();

            TempData["Success"] = "Order Details Updated Successfully.";
            return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });
        }
        [HttpPost]
        //[Authorize(Roles = SD.Role_Admin + " " + SD.Role_Employee)] not working
        public IActionResult StartProcessing(OrderViewModel model)
        {
            _unitOfWork.OrderHeader.UpdateStatus(model.OrderHeader.Id, SD.StatusInProcess);
            TempData["Success"] = "Order Details Updated Successfully.";
            _unitOfWork.Save();
            return RedirectToAction(nameof(Details), new { orderId = model.OrderHeader.Id });
        }

        [HttpPost]
        //[Authorize(Roles = SD.Role_Admin + " " + SD.Role_Employee)] not working
        public IActionResult ShipOrder(OrderViewModel model)
        {
            var orderHeader = _unitOfWork.OrderHeader.Get(x => x.Id == model.OrderHeader.Id);
            orderHeader.TrackingNumber = model.OrderHeader.TrackingNumber;
            orderHeader.Carrier = model.OrderHeader.Carrier;
            orderHeader.OrderStatus = SD.StatusShipped;
            orderHeader.ShippingDate = DateTime.Now;
            if(orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                orderHeader.PaymentDueDate = DateTime.Now.AddDays(30);
            }
            
            _unitOfWork.OrderHeader.Update(orderHeader);
           
            _unitOfWork.Save();
            TempData["Success"] = "Order Shipped Successfully.";
            return RedirectToAction(nameof(Details), new { orderId = model.OrderHeader.Id });
        }

        [HttpPost]
        //[Authorize(Roles = SD.Role_Admin + " " + SD.Role_Employee)] not working
        public IActionResult CancelOrder(OrderViewModel model)
        {
            var orderHeader = _unitOfWork.OrderHeader.Get(x => x.Id == model.OrderHeader.Id);

            if (orderHeader.PaymentStatus == SD.PaymentStatusApproved)
            {
                var options = new RefundCreateOptions
                {
                    Reason = RefundReasons.RequestedByCustomer,
                    PaymentIntent = orderHeader.PaymentIntendId
                };


                var service = new RefundService();
                Refund refund = service.Create(options);

                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusRefunded);
            }
            else
            {
                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);

            }
            _unitOfWork.Save();
            TempData["Success"] = "Order Cancelled Successfully.";
            return RedirectToAction(nameof(Details), new { orderId = model.OrderHeader.Id });
        }

        
        [ActionName("Details")]
        [HttpPost]
        public IActionResult Details_PAY_NOW(OrderViewModel OrderVM)
        {
            OrderVM.OrderHeader = _unitOfWork.OrderHeader
                .Get(u => u.Id == OrderVM.OrderHeader.Id, includeProperties: "ApplicationUser");
            OrderVM.OrderDetails = _unitOfWork.OrderDetail
                .GetAll(u => u.OrderHeaderId == OrderVM.OrderHeader.Id, includeproperties: "Product");

            //stripe logic
            var domain = Request.Scheme + "://" + Request.Host.Value + "/";
            var options = new SessionCreateOptions
            {
                SuccessUrl = domain + $"admin/order/PaymentConfirmation?orderHeaderId={OrderVM.OrderHeader.Id}",
                CancelUrl = domain + $"admin/order/details?orderId={OrderVM.OrderHeader.Id}",
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
            };

            foreach (var item in OrderVM.OrderDetails)
            {
                var sessionLineItem = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Price * 100), // $20.50 => 2050
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Title
                        }
                    },
                    Quantity = item.Count
                };
                options.LineItems.Add(sessionLineItem);
            }


            var service = new SessionService();
            Session session = service.Create(options);
            _unitOfWork.OrderHeader.UpdateStripePaymentId(OrderVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
            _unitOfWork.Save();
            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }

        public IActionResult PaymentConfirmation(int orderHeaderId)
        {

            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderHeaderId);
            if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                //this is an order by company

                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);

                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _unitOfWork.OrderHeader.UpdateStripePaymentId(orderHeaderId, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHeader.UpdateStatus(orderHeaderId, orderHeader.OrderStatus, SD.PaymentStatusApproved);
                    _unitOfWork.Save();
                }


            }


            return View(orderHeaderId);
        }

        #region API Calls
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<OrderHeader> objOrdersHeaders; 
            if(User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
            {
                objOrdersHeaders = _unitOfWork.OrderHeader.GetAll(includeproperties: "ApplicationUser").ToList();
            }
            else
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
                objOrdersHeaders = _unitOfWork.OrderHeader.GetAll(x => x.ApplicationUserId == userId, includeproperties: "ApplicationUser");
            }
            switch (status)
            {
                case "pending":
                    objOrdersHeaders = objOrdersHeaders.Where(x => x.PaymentStatus == SD.PaymentStatusDelayedPayment);
                    break;
                case "inprocess":
                    objOrdersHeaders = objOrdersHeaders.Where(x => x.OrderStatus == SD.StatusInProcess);
                    break;
                case "completed":
                    objOrdersHeaders = objOrdersHeaders.Where(x => x.OrderStatus == SD.StatusShipped);
                    break;
                case "approved":
                    objOrdersHeaders = objOrdersHeaders.Where(x => x.OrderStatus == SD.StatusApproved);
                    break;
                default:
                    break;
            }
            return Json(new { data = objOrdersHeaders });

        }
        #endregion

    }
}
