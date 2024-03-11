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
using Newtonsoft.Json.Linq;
using Dapper;

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
                    string pID = paymentUpdate.Id;
                    string sQuery = "";
                    long CompID = 0;
                    long TransNo = 0;
                    long InvoiceTransNo = 0;
                    string? defARDiscAcct = "";
                    string? invoiceTransAccount = "";

                    decimal? totalPmtsApplied = null;
                    decimal? totalDiscApplied = null;
                    var DiscountID = 0;

                    using (var connection = new SqlConnection(gsSQLConnWrite))
                    {
                        await connection.OpenAsync();

                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {



                                // Retrieve the DefARDiscAcct based on CompanyID
                                sQuery = "SELECT TOP 1 IDNo, Company_ID, TransNo, TransAccount, TransEntityID, TransEntityName, TransDocRef, RefExternal1, ISNULL(TransBalDue,0) AS TransBalDue, ISNULL(TransDiscElig,0) AS TransDiscElig FROM GL_TransMain WHERE StripePaymentID = @StripePaymentID";
                                var transData = await connection.QueryFirstOrDefaultAsync(sQuery, new { StripePaymentID = pID }, transaction);
                                if (transData != null)
                                {
                                    CompID = transData.Company_ID;
                                    InvoiceTransNo = transData.TransNo;
                                }

                                //TransNo For AR0 Record
                                var TransNoTemp = await GetNextSrvNo(CompID);
                                if (TransNoTemp == null) TransNoTemp = 0;

                                TransNo = TransNoTemp;

                                // Retrieve the DefARDiscAcct based on CompanyID
                                sQuery = "SELECT DefARDiscAcct FROM Set_AcctDefaults WHERE Company_ID = @CompanyID";
                                defARDiscAcct = await connection.QueryFirstOrDefaultAsync<string?>(sQuery, new { CompanyID = CompID });

                                //// Get Starting Totals
                                //sQuery = "SELECT SUM(TransCreditAmount) AS [TotalPayments] FROM GL_TransMain WHERE TransNo = @TransNo AND TransType = 'ARP' AND Company_ID = @CompanyID AND TransCreditAmount IS NOT NULL";
                                //totalPmtsApplied = await connection.QueryFirstOrDefaultAsync<decimal?>(sQuery, new { TransNo, CompanyID = CompID });
                                //if (totalPmtsApplied == null) totalPmtsApplied = 0.00M;

                                //sQuery = "SELECT SUM(TransCreditAmount) AS [TotalDiscounts] FROM GL_TransMain WHERE TransNo = @TransNo AND TransType = 'ARD' AND Company_ID = @CompanyID AND TransCreditAmount IS NOT NULL";
                                //totalDiscApplied = await connection.QueryFirstOrDefaultAsync<decimal?>(sQuery, new { TransNo, CompanyID = CompID });
                                //if (totalDiscApplied == null) totalDiscApplied = 0.00M;

                                ////CHECK TO SEE IF THERE IS AN DISCOUNT RECORD ALREADY EXISTS
                                //sQuery = $"SELECT IDNo FROM GL_TransMain WHERE Company_ID = @CompanyID AND TransNo = @TransNo AND LinkBackID = @LinkBackID AND TransType = 'ARD'";
                                //var DiscountID = await connection.QueryFirstOrDefaultAsync<long?>(sQuery, new { CompanyID = CompID, TransNo = payload.TransNo, LinkBackID = transData.TransNo }, transaction);
                                //if (DiscountID == null) DiscountID = 0;

                                if (transData.TransBalDue - transData.TransDiscElig != 0)
                                {

                                    // Create ARP record
                                    sQuery = "INSERT INTO GL_TransMain (Company_ID, InputUserName, InputDT, TransType, TransDate, TransCreditAmount, TransNo, TransDocRef, RefExternal1, LinkBackID, TransEntityID, TransEntityName, TransAccount)" +
                                                    " VALUES (@CompanyID, @UserName, @CurrentTime, 'ARP', @TransDate, @TransCreditAmount, @TransNo, @TransDocRef, @RefExternal1, @LinkBackID, @TransEntityID, @TransEntityName, @TransAccount)";
                                    if (DiscountID == 0)
                                    {
                                        await connection.ExecuteAsync(sQuery, new
                                        {
                                            CompanyID = CompID,
                                            UserName = "SysUser",
                                            CurrentTime = DateTime.Now,
                                            DateTime.Now, //payload.TransDate,
                                            TransNo,
                                            transData.TransDocRef,
                                            transData.RefExternal1,
                                            LinkBackID = transData.TransNo,
                                            transData.TransEntityID,
                                            transData.TransEntityName,
                                            transData.TransAccount,
                                            TransCreditAmount = transData.TransBalDue - transData.TransDiscElig
                                        }, transaction);
                                        totalPmtsApplied = totalPmtsApplied + (transData.TransBalDue - transData.TransDiscElig);
                                    }
                                    else
                                    {
                                        await connection.ExecuteAsync(sQuery, new
                                        {
                                            CompanyID = CompID,
                                            UserName = "SysUser",
                                            CurrentTime = DateTime.Now ,
                                            DateTime.Now, //payload.TransDate,
                                            TransNo,
                                            transData.TransDocRef,
                                            transData.RefExternal1,
                                            LinkBackID = transData.TransNo,
                                            transData.TransEntityID,
                                            transData.TransEntityName,
                                            transData.TransAccount,
                                            TransCreditAmount = transData.TransBalDue
                                        }, transaction);
                                        totalPmtsApplied = totalPmtsApplied + transData.TransBalDue;

                                    }
                                }

                                // Create ARD records if TransDiscElig is present
                                if (transData.TransDiscElig != 0)
                                {

                                    if (DiscountID == 0)
                                    {
                                        sQuery = "INSERT INTO GL_TransMain (Company_ID, InputUserName, InputDT, TransType, TransDate, TransCreditAmount, TransNo, TransDocRef, RefExternal1, LinkBackID, TransEntityID, TransEntityName, TransAccount)" +
                                                    " VALUES (@CompanyID, @UserName, @CurrentTime, 'ARD', @TransDate, @TransCreditAmount, @TransNo, @TransDocRef, @RefExternal1, @LinkBackID, @TransEntityID, @TransEntityName, @TransAccount)";
                                        await connection.ExecuteAsync(sQuery, new
                                        {
                                            CompanyID = CompID,
                                            UserName = "SysUser",
                                            CurrentTime = DateTime.Now,
                                            DateTime.Now, //payload.TransDate,
                                            TransNo,
                                            LinkBackID = transData.TransNo,
                                            transData.TransDocRef,
                                            transData.RefExternal1,
                                            transData.TransEntityID,
                                            transData.TransEntityName,
                                            transData.TransAccount,
                                            TransCreditAmount = transData.TransDiscElig
                                        }, transaction);
                                        totalDiscApplied = totalDiscApplied + transData.TransDiscElig;

                                        sQuery = "INSERT INTO GL_TransMain (Company_ID, InputUserName, InputDT, TransType, TransDate, TransDebitAmount, TransNo, TransDocRef, RefExternal1, LinkBackID, TransEntityID, TransEntityName, TransAccount)" +
                                                        " VALUES (@CompanyID, @UserName, @CurrentTime, 'ARD', @TransDate, @TransDebitAmount, @TransNo, @TransDocRef, @RefExternal1, @LinkBackID, @TransEntityID, @TransEntityName, @TransAccount)";
                                        await connection.ExecuteAsync(sQuery, new
                                        {
                                            CompanyID = CompID,
                                            UserName = "SysUser",
                                            CurrentTime = DateTime.Now,
                                            DateTime.Now, //payload.TransDate,
                                            TransNo,
                                            transData.TransDocRef,
                                            transData.RefExternal1,
                                            LinkBackID = transData.TransNo,
                                            transData.TransEntityID,
                                            transData.TransEntityName,
                                            TransAccount = defARDiscAcct,
                                            TransDebitAmount = transData.TransDiscElig
                                        }, transaction);

                                    }
                                }

                                // Update TransBalDue on I0
                                sQuery = "UPDATE GL_TransMain SET TransBalDue = 0 WHERE IDNo = @IDNo AND Company_ID = @CompanyID";
                                await connection.ExecuteAsync(sQuery, new
                                {
                                    CompanyID = CompID,
                                    transData.IDNo
                                }, transaction);


                                // Update AR0 records
                                sQuery = "UPDATE GL_TransMain SET TotalDiscApplied = @TotalDiscApplied, TransDebitAmount = @TotalPmtsApplied WHERE TransNo = @TransNo AND TransType = 'AR0' AND Company_ID = @CompanyID";
                                await connection.ExecuteAsync(sQuery, new
                                {
                                    TotalPmtsApplied = totalPmtsApplied,
                                    TotalDiscApplied = totalDiscApplied,
                                    CompanyID = CompID,
                                    TransNo
                                }, transaction);


                                transaction.Commit();


                                //var cmd = new SqlCommand("", connection);
                                //cmd.Parameters.AddWithValue("@StripePaymentID", pID);

                                //cmd.CommandText = $"UPDATE GL_TransMain SET ChkClearedYN = 1 WHERE StripePaymentID = @StripePaymentID";
                                //await cmd.ExecuteNonQueryAsync();

                                //  }
                                //    catch
                                //    {
                                //        transaction.Rollback();
                                //        throw;
                                //    }
                                //}
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
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
             
    }


        public static async Task<long> GetNextSrvNo(long CompID)
        {
            string sQuery = "";

            int maxRetries = 5;
            int delayBetweenRetries = 100;  // in milliseconds, 100ms = 0.1 second
            int currentAttempt = 0;

            while (currentAttempt < maxRetries)
            {
                try
                {
                    ///Validate SQL
                    string tableName = "";
                    string fieldName = "";
                    string gsSQLConnWrite = "Server=dbmain-101.cg6examucvdy.us-east-2.rds.amazonaws.com;Database=CDB_001;User ID=Readwrite8372;Password=*scTN8!tt6jyfR!4G!K;TrustServerCertificate=True;Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";


                    tableName = "Srv_TransNo";
                    fieldName = "TransNo";
                


                    // Build SQL               
                    var whereClause = $"WHERE Company_ID = @CID";
                    sQuery = $"SELECT {fieldName} FROM {tableName} WITH (UPDLOCK) {whereClause}";

                    using (var connection = new SqlConnection(gsSQLConnWrite))
                    {
                        await connection.OpenAsync();

                        // Start a transaction to lock the row.
                        using (var transaction = connection.BeginTransaction())
                        {
                            Int64? currentNo = await connection.QuerySingleOrDefaultAsync<Int64?>(sQuery, new { CID = CompID }, transaction: transaction) ?? null;

                            if (currentNo == null) // Record does not exist
                            {

                                sQuery = $"INSERT INTO {tableName} ({fieldName}, Company_ID) VALUES (@DefaultValue, @CID)";
                                await connection.ExecuteAsync(sQuery, new { DefaultValue = 100, CID = CompID }, transaction: transaction);

                                // Since the default value is already 100, you can just return it.
                                transaction.Commit();
                                return 100;
                            }
                            else
                            {
                                Int64? newNo = currentNo + 1;

                                sQuery = $"UPDATE {tableName} SET {fieldName} = @NewNo {whereClause}";

                                await connection.ExecuteAsync(sQuery, new { NewNo = newNo, CID = CompID }, transaction: transaction);

                                // Commit the transaction to unlock the row.
                                transaction.Commit();

                                return (Int64)newNo;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    currentAttempt++;
                    if (currentAttempt == maxRetries)  // If reached the max retries, handle the exception.
                    {

                        return 0;
                    }

                    // Wait before retrying
                    await Task.Delay(delayBetweenRetries);
                }
            }
            return 0;
        }



    }
}