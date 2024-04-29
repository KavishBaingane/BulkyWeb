using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
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
        #region API Calls
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<OrderHeader> objOrdersHeaders = _unitOfWork.OrderHeader.GetAll(includeproperties: "ApplicationUser").ToList();
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
