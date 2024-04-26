﻿using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

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
            var cartFromDb = _unitOfWork.ShoppingCart.Get(x => x.Id == cartId);
            if (cartFromDb.Count <= 1)
            {
                //in this we are removing it from the dataBase table as it is going zero
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                //decreemenitng the value it to the table/update 
                cartFromDb.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
            }
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(x => x.Id == cartId);
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