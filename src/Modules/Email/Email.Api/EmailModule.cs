using Email.Application;
using Email.Application.Features.EmailSender;
using Email.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Email.Api;

public static class EmailModule
{
    public static IServiceCollection AddEmailModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddEmailInfrastructure(config);
        services.AddEmailApplication();
        return services;
    }

    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var emailGroup = endpoints.MapGroup("/api/email").WithTags("Email API");
        // Health check endpoint
        emailGroup.MapGet("/health", async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync("Email API is healthy.");
        });
        #region GET Endpoints
        #endregion

        #region POST Endpoints
        emailGroup.MapPost("/send", async (EmailRequest request, IMediator mediator) =>
        {
            var command = new EmailSenderCommand(request.To, request.Subject, request.Body);
            //command handler will handle the request and send the RabbitMQ message to the Email Worker
            try
            {
                var jobId = await mediator.Send(command);
                return Results.Ok($"Email sent to RabbitMQ successfully. Job ID: {jobId}");
            }
            catch (Exception ex)
            {
                // Log the exception (not implemented here)
                return Results.Problem($"An error occurred while sending the email to RabbitMQ. :{ex.Message}", statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }).WithName("SendEmail").WithDescription("Sends an email based on the provided request via RabbitMQ.");
        #endregion
        
        #region PUT Endpoints
        #endregion

        #region PATCH Endpoints
        #endregion
        
        #region DELETE Endpoints
        #endregion


        return endpoints;
    }
    public record EmailRequest(string To, string Subject, string Body);
}
