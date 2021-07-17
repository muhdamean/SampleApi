using SampleApi.Configurations;
using SampleApi.Data;
using SampleApi.Dtos;
using SampleApi.Dtos.Requests;
using SampleApi.Dtos.Responses;
using SampleApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SampleApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthManagementController : ControllerBase
    {
        private readonly AppDbContext appDbContext;
        private readonly UserManager<IdentityUser> userManager;
        private readonly TokenValidationParameters tokenValidationParameters;
        private readonly JwtConfig jwtConfig;

        public AuthManagementController(AppDbContext appDbContext, UserManager<IdentityUser> userManager,IOptionsMonitor<JwtConfig> optionsMonitor, TokenValidationParameters tokenValidationParameters)
        {
            this.appDbContext = appDbContext;
            this.userManager = userManager;
            this.tokenValidationParameters = tokenValidationParameters;
            jwtConfig = optionsMonitor.CurrentValue;
        }
        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDto user)
        {
            if (ModelState.IsValid)
            {
                var existingUser =await userManager.FindByEmailAsync(user.Email);
                if (existingUser!=null)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors = new List<string>(){ "Email already in use" },
                        Success = false
                    });
                }
                var newUser = new IdentityUser { Email = user.Email, UserName = user.Email };
                var isCreated = await userManager.CreateAsync(newUser, user.Password);
                if (isCreated.Succeeded)
                {
                    var jwtToken =await GenerateJwtToken(newUser);
                    return Ok(jwtToken);
                }
                else
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors =  isCreated.Errors.Select(x=>x.Description).ToList(),
                        Success = false
                    });
                }
            }
            return BadRequest(new RegistrationResponse()
            {
                Errors = new List<string>(){"Invalid payload"},
                Success=false
            });
        }
        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto user)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await userManager.FindByEmailAsync(user.Email);
                if (existingUser==null)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors = new List<string>() { "Invalid login request" },
                        Success = false
                    });
                }
                var isCorrect = await userManager.CheckPasswordAsync(existingUser, user.Password);
                if (!isCorrect)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors = new List<string>() { "Invalid login request" },
                        Success = false
                    });
                }
                var jwtToken =await GenerateJwtToken(existingUser);
                return Ok(jwtToken);
            }
            return BadRequest(new RegistrationResponse()
            {
                Errors = new List<string>() { "Invalid payload" },
                Success = false
            });
        }
        [HttpPost]
        [Route("RefreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenRequestDto tokenRequest)
        {
            if (ModelState.IsValid)
            {
                var result= await VerifyAndGenerateToken(tokenRequest);
                if (result==null)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Errors = new List<string>() { "Invalid tokens" },
                        Success = false
                    });
                }
                return Ok(result);
            }
            return BadRequest(new RegistrationResponse()
            {
                Errors = new List<string>() { "Invalid payload" },
                Success = false
            });
        }
        private async Task<AuthResult> GenerateJwtToken(IdentityUser user)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtConfig.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("Id",user.Id),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Sub,user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                }),
                Expires=DateTime.UtcNow.AddSeconds(30),
                SigningCredentials=new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = jwtTokenHandler.WriteToken(token);
            var refreshToken = new RefreshToken()
            {
                JwId = token.Id,
                IsUsed = false,
                IsRevoked = false,
                UserId = user.Id,
                AddedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                Token = RandomString(35) + Guid.NewGuid()
            };
            await appDbContext.RefreshTokens.AddAsync(refreshToken);
            await appDbContext.SaveChangesAsync();

            return new AuthResult() { 
                Success=true,
                Token=jwtToken,
                RefreshToken= refreshToken.Token,
                
            };
        }
        private async Task<AuthResult> VerifyAndGenerateToken(TokenRequestDto tokenRequest)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            try
            {
                //Validation 1 - JWT token format
                var tokenInVerification = jwtTokenHandler.ValidateToken(tokenRequest.Token, tokenValidationParameters, out var validatedToken);
                //Validation 2 - Validate encryption algorithm
                if (validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);
                    if (result==false)
                    {
                        return null;
                    }
                }
                //Validation 3 - Validate expiry date
                var utcExpiryDate = long.Parse(tokenInVerification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp).Value);
                var expiryDate = UnixTimeStampToDateTime(utcExpiryDate);
                if (expiryDate>DateTime.UtcNow)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Token has not yet expired" }
                    };
                }
                //Validation 4 - Validate existence of the token
                var storedToken = await appDbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == tokenRequest.RefreshToken);
                if (storedToken==null)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Token does not exist" }
                    };
                }
                //Validation 5 - Validate if used
                if (storedToken.IsUsed)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Token has been used" }
                    };
                }
                //Validation 6 - Validate if revoked
                if (storedToken.IsRevoked)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Token has been revoked" }
                    };
                }
                //Validation 7 - Validate the id
                var jti = tokenInVerification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti).Value;
                if (storedToken.JwId!=jti)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Token does not match" }
                    };
                }

                //Update Current Token 
                storedToken.IsUsed = true;appDbContext.RefreshTokens.Update(storedToken);
                await appDbContext.SaveChangesAsync();

                //Generate a new token
                var dbUser = await userManager.FindByIdAsync(storedToken.UserId);
                return await GenerateJwtToken(dbUser);
            }
            catch (Exception)
            {
                return null;
            }
        }
        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTimeVal = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTimeVal = dateTimeVal.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dateTimeVal;
        }
        private string RandomString(int length)
        {
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(x => x[random.Next(x.Length)]).ToArray());
        }
    }
}
