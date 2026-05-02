using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ResumeScreening.API.Helpers
{
    public static class ClaimsExtensions
    {
        /// <summary>
        /// Resolves user id from common JWT claim types (MapInboundClaims off leaves "sub"; on uses NameIdentifier).
        /// </summary>
        public static int? GetUserId(this ClaimsPrincipal user)
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                     ?? user.FindFirstValue("sub")
                     ?? user.FindFirstValue("nameid")
                     ?? user.FindFirstValue("uid");
            return int.TryParse(id, out var value) ? value : null;
        }
    }
}
