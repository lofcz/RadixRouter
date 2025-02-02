using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using BlazingRouter.Demo.Components;
using BlazingRouter.Demo.Pages.Home;

namespace BlazingRouter.Demo;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddMemoryCache();
        builder.Services.AddSignalR();

        // Add services to the container.
        builder.Services.AddBlazingRouter()
            .Configure(x =>
            {
                x.HasRole = (principal, role) =>
                {
                    Claim? firstClaim = principal.Claims.FirstOrDefault();

                    if (firstClaim is null)
                    {
                        return false;
                    }

                    return firstClaim.Value == ((int)role).ToString();
                };

                x.OnSetupAllowedUnauthorizedRoles = () =>
                {
                    return [ "/home/unauthorized" ];
                };

                x.OnRedirectUnauthorized = (user, route) =>
                {
                    return "/home/unauthorized";
                };
                
                x.OnPageScanned = (type) =>
                {
                    // add custom routes here
                    if (type == typeof(CustomRoute))
                    {
                        Route customRoute = new Route("/this/is/custom", type);
                        return [ customRoute ];   
                    }

                    return [];
                };

                x.OnTypeDiscovered = (type) =>
                {
                    return null;
                };
            })
            .Build();
        
        builder.Services.AddMvc(x => x.EnableEndpointRouting = false);
        builder.Services.AddRazorPages();
        
        builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddAuthentication(x =>
        {
            x.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        }).AddCookie(x =>
        {
            x.SlidingExpiration = true;
            x.ExpireTimeSpan = TimeSpan.FromDays(365);
            x.LoginPath = "/home/index";
            x.LogoutPath = "/home/index";
            x.Cookie.SameSite = SameSiteMode.None;
            x.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            x.Cookie.MaxAge = TimeSpan.FromDays(365);
        });
        
        builder.Services.AddAuthorizationCore();
        
        builder.Services.ConfigureExternalCookie(options =>
        {
            options.Cookie.SameSite = SameSiteMode.None;
        });
        
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddHubOptions(x =>
            {
                x.MaximumReceiveMessageSize = 32 * 1024;
            });


        WebApplication app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        
        app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMvcWithDefaultRoute();
        
        app.UseEndpoints(x =>
        {
            x.MapBlazorHub();
            x.MapFallbackToPage("/_Host");
        });

        app.Run();
    }
}