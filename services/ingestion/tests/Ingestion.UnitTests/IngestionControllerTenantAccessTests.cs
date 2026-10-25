using System.Security.Claims;
using Ingestion.Api.Controllers;
using Ingestion.Api.Security;
using Ingestion.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace Ingestion.UnitTests;

public class IngestionControllerTenantAccessTests
{
    private const string UserIssuer = "sentinel";

    private readonly Mock<IMediator> _mockMediator = new();

    private IngestionController CreateController(params Guid[] tenantClaims)
    {
        var claims = new List<Claim> { new("sub", "user-1"), new("iss", UserIssuer) };
        claims.AddRange(tenantClaims.Select(tenantId => new Claim("tenant_id", tenantId.ToString())));
        var tenantAccess = new TenantAccessEvaluator(Options.Create(new JwtOptions { Issuer = UserIssuer }));

        return new IngestionController(_mockMediator.Object, tenantAccess)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
                }
            }
        };
    }

    [Fact]
    public async Task RegisterClimaticEvent_ForTenantNotInClaims_ReturnsForbidAndDoesNotDispatch()
    {
        var controller = CreateController(Guid.NewGuid());
        var command = new RegisterClimaticEventCommand { TenantId = Guid.NewGuid(), Payload = "{}" };

        var result = await controller.RegisterClimaticEvent(command);

        Assert.IsType<ForbidResult>(result.Result);
        _mockMediator.Verify(m => m.Send(It.IsAny<RegisterClimaticEventCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterDataSource_ForTenantNotInClaims_ReturnsForbid()
    {
        var controller = CreateController();
        var command = new RegisterDataSourceCommand { TenantId = Guid.NewGuid() };

        var result = await controller.RegisterDatasource(command);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task RegisterSensorCollection_ForOwnTenant_DispatchesCommand()
    {
        var tenantId = Guid.NewGuid();
        var controller = CreateController(tenantId);
        var command = new RegisterSensorCollectionCommand { TenantId = tenantId, Payload = "{}" };
        _mockMediator.Setup(m => m.Send(command, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());

        var result = await controller.RegisterSensorCollection(command);

        Assert.IsType<CreatedResult>(result.Result);
        _mockMediator.Verify(m => m.Send(command, It.IsAny<CancellationToken>()), Times.Once);
    }
}
