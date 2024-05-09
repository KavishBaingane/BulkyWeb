using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Model;
using Stripe.Checkout;
using System.Linq;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public ShoppingCartViewModel ShoppingCartViewModel { get; set; }
        public CartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartViewModel model = new ShoppingCartViewModel();
            model.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == userId, includeproperties: "Product");
            model.OrderHeader = new OrderHeader();
            foreach (var cart in model.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                model.OrderHeader.OrderTotal += cart.Price * cart.Count;
            }
            return View(model);
        }

        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(x => x.Id == cartId);
            cartFromDb.Count += 1;
            _unitOfWork.ShoppingCart.Update(cartFromDb);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(x => x.Id == cartId, tracked: true);
            if (cartFromDb.Count <= 1)
            {
                //in this we are removing it from the dataBase table as it is going zero
                HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                //decreementing the value it to the table/update 
                cartFromDb.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
            }
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(x => x.Id == cartId, tracked:true);
            HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
            _unitOfWork.ShoppingCart.Remove(cartFromDb);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartViewModel model = new ShoppingCartViewModel();
            model.OrderHeader = new OrderHeader();
            model.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == userId, includeproperties: "Product");
            model.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
            model.OrderHeader.Name = model.OrderHeader.ApplicationUser.Name;
            model.OrderHeader.PhoneNumber = model.OrderHeader.ApplicationUser.PhoneNumber;
            model.OrderHeader.StreetAddress = model.OrderHeader.ApplicationUser.StreetAddress;
            model.OrderHeader.City = model.OrderHeader.ApplicationUser.City;
            model.OrderHeader.State = model.OrderHeader.ApplicationUser.State;
            model.OrderHeader.PoastalCode = model.OrderHeader.ApplicationUser.PostalCode;
            foreach (var cart in model.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                model.OrderHeader.OrderTotal += cart.Price * cart.Count;
            }
            return View(model);
        }

        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPOST(ShoppingCartViewModel model)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            //ShoppingCartViewModel model = new ShoppingCartViewModel();
            //model.OrderHeader = new OrderHeader();
            model.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == userId, includeproperties: "Product");
            model.OrderHeader.OrderDate = DateTime.Now;
            model.OrderHeader.ApplicationUserId = userId;
            ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
            foreach (var cart in model.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                model.OrderHeader.OrderTotal += cart.Price * cart.Count;
            }
            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                //it is the regular customer account and we need to capture Payment
                model.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
                model.OrderHeader.OrderStatus = SD.StatusPending;
            }
            else
            {
                //it is a company account
                model.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                model.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
            }
            _unitOfWork.OrderHeader.Add(model.OrderHeader);
            _unitOfWork.Save();

            foreach (var item in model.ShoppingCartList)
            {
                OrderDetail orderDetail = new()
                {
                    ProductId = item.ProductId,
                    OrderHeaderId = model.OrderHeader.Id,
                    Price = item.Price,
                    Count = item.Count,
                };
                _unitOfWork.OrderDetail.Add(orderDetail);
                _unitOfWork.Save();
            }

            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                //it is a regular customer account and we need to capture payment 
                //stripe logic
                var domain = "https://localhost:44302/";
                var options = new SessionCreateOptions
                {

                    SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={model.OrderHeader.Id}",
                    CancelUrl = domain + $"customer/cart/Index",
                    Mode = "payment",
                };

                options.LineItems = new List<SessionLineItemOptions>(); //initialise as a list
                foreach (var item in model.ShoppingCartList)
                {
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Price * 100), //$20.50 => 250
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
                Session session = service.Create(options); //response of session 
                _unitOfWork.OrderHeader.UpdateStripePaymentId(model.OrderHeader.Id, session.Id, session.PaymentIntentId);
                _unitOfWork.Save();
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);
            }

            return RedirectToAction(nameof(OrderConfirmation), new { id = model.OrderHeader.Id });
        }

        public IActionResult OrderConfirmation(int id)
        {
            //adding logic for make we know that the order is successful or not by retrieving the info via sessionId
            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(x => x.Id == id, includeProperties: "ApplicationUser");
            if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                //this is an order by a CUstomer
            }
            var service = new SessionService();
            Session session = service.Get(orderHeader.SessionId);

            if (session.PaymentStatus.ToLower() == "paid")
            {
                _unitOfWork.OrderHeader.UpdateStripePaymentId(id, session.Id, session.PaymentIntentId);
                _unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                _unitOfWork.Save();
            }

            List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == orderHeader.ApplicationUserId).ToList();


            _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            _unitOfWork.Save();
            return View(id);
        }

        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                {
                    if (shoppingCart.Count <= 100)
                    {
                        return shoppingCart.Product.Price50;
                    }
                    else
                    {
                        return shoppingCart.Product.Price100;
                    }
                }
            }
        }

    }
}
