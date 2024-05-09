using BulkyBook.DataAccess;
using BulkyBook.DataAccess.Repository;
using BulkyBook.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using BulkyBook.Models;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Identity.UI.Services;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
    ));
//for Strip Payment Starts
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

//for Strip Payment Ends

builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IEmailSender, EmailSender>();

//for redirecting if the page is unauthorized for the user , then show them these as per conditions ((add after identity remember this))
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = $"/Identity/Account/Login";
    options.LogoutPath = $"/Identity/Account/Logout";
    options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
});

//for adding facebook authentication
builder.Services.AddAuthentication().AddFacebook(options =>
{
    options.AppId = "1021678569425601";
    options.AppSecret = "c2be2a169f79550cf9c2a44821c50a6e";
});


//for adding session to the services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(100);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

//for configuration of stripe for secretKey and publishable key
StripeConfiguration.ApiKey = builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();

app.UseRouting();
app.UseAuthentication();

app.UseAuthorization();
//after auhtorzation we need to add session to the request pipeline too
app.UseSession();

app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");

app.Run();
