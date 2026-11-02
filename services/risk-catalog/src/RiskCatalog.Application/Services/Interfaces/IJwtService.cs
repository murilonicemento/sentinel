using System.Security.Claims;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Services.Interfaces;

public interface IJwtService
{
    public string GenerateJwt(IdentityDTO identityDTO);
}