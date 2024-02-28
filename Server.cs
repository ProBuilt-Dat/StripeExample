using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Stripe;
using System.Data.SqlClient;

namespace StripeExample
{
  public class Program
  {
    public static void Main(string[] args)
    {
      WebHost.CreateDefaultBuilder(args)
        .UseUrls("http://0.0.0.0:4242")
        .UseWebRoot("public")
        .UseStartup<Startup>()
        .Build()
        .Run();
    }
  }

  public class Startup
  {
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddMvc().AddNewtonsoftJson();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      // This is your test secret API key.
      StripeConfiguration.ApiKey = "sk_test_51OjUfSGUX3JyrV1mg3sCzlYYI7VMyIcbQNv2aBPokEos0lrT3AEIFHnsoT2wFzguzSN2uXd1RzCe0kBPIidSpxrA00nDLUtjrs";

      if (env.IsDevelopment()) app.UseDeveloperExceptionPage();
      app.UseRouting();
      app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
  }

  [Route("webhook")]
  [ApiController]
  public class WebhookController : Controller
  {
    [HttpPost]
    public async Task<IActionResult> Index()
    {

            string gsSQLConnWrite = "Server=dbmain-101.cg6examucvdy.us-east-2.rds.amazonaws.com;Database=CDB_001;User ID=Readwrite8372;Password=*scTN8!tt6jyfR!4G!K;TrustServerCertificate=True;Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";
            string gsSQLConnRead = "Server=dbmain-101.cg6examucvdy.us-east-2.rds.amazonaws.com;Database=CDB_001;User ID=Readonly8724;Password=9fV8-sYfrH@xJMiFE@K;TrustServerCertificate=True;Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";

            
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        const string endpointSecret = "whsec_...";
        try
        {
            var stripeEvent = EventUtility.ParseEvent(json);
            var signatureHeader = Request.Headers["Stripe-Signature"];

            stripeEvent = EventUtility.ConstructEvent(json,
                    signatureHeader, endpointSecret);

            if (stripeEvent.Type == Events.PaymentIntentSucceeded)
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                Console.WriteLine("A successful payment for {0} was made.", paymentIntent.Amount);
                // Then define and call a method to handle the successful payment intent.
                // handlePaymentIntentSucceeded(paymentIntent);
            }
            else if (stripeEvent.Type == Events.PaymentMethodAttached)
            {
                var paymentMethod = stripeEvent.Data.Object as PaymentMethod;
                // Then define and call a method to handle the successful attachment of a PaymentMethod.
                // handlePaymentMethodAttached(paymentMethod);
            }
            else if (stripeEvent.Type == Events.PaymentLinkCreated)
            {
            
            }
            else if (stripeEvent.Type == Events.PaymentLinkUpdated)
            {

                    var paymentUpdate = stripeEvent.Data.Object as PaymentLink;
                    //paymentUpdate.i

                    using (var connection = new SqlConnection(gsSQLConnWrite))
                    {
                        await connection.OpenAsync();
                        var cmd = new SqlCommand("", connection);
                    }
            }
                else
            {
                Console.WriteLine("Unhandled event type: {0}", stripeEvent.Type);
            }
            return Ok();
        }
        catch (StripeException e)
        {
            Console.WriteLine("Error: {0}", e.Message);
            return BadRequest();
        }
        catch (Exception e)
        {
          return StatusCode(500);
        }
    }
  }
}