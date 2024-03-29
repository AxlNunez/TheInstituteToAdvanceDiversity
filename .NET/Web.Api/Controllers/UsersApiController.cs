﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sabio.Models;
using Sabio.Models.Domain.Users;
using Sabio.Models.Requests.Users;
using Sabio.Services;
using Sabio.Web.Controllers;
using Sabio.Web.Core;
using Sabio.Web.Models.Responses;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Sabio.Web.Api.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersApiController : BaseApiController
    {
        private IUserService _service = null;
        private IAuthenticationService<int> _authService = null;
        private IEmailService _emailService = null;

        public UsersApiController(IUserService service
            , ILogger<PingApiController> logger
            , IAuthenticationService<int> authService
            , IEmailService emailService
            ) : base(logger)
        {
            _service = service;
            _authService = authService;
            _emailService = emailService;
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<SuccessResponse>> LoginAsync(VerifyUserEmailPassword model)
        {
            int code = 200;
            BaseResponse response = null;
            bool success = false;
            try
            {
                success = await _service.LogInAsync(model.Email, model.Password);
                if (!success)
                {
                    code = 401;
                    response = new ErrorResponse("Login Unsuccessful");
                }
                else
                {
                    response = new SuccessResponse();
                }
            }
            catch (Exception ex)
            {
                code = 500;
                response = new ErrorResponse(ex.Message);
                base.Logger.LogError(ex.ToString());
            }
            return StatusCode(code, response);
        }
        [HttpGet("logout")]
        public async Task<ActionResult<SuccessResponse>> LogoutAsync()
        {
            int code = 200;
            BaseResponse response = null;
            try
            {
                await _authService.LogOutAsync();
                response = new SuccessResponse();
            }
            catch (Exception ex)
            {
                code = 500;
                base.Logger.LogError(ex.ToString());
                response = new ErrorResponse(ex.Message);
            }
            return StatusCode(code, response);
        }
        [HttpGet("current")]
        public ActionResult<ItemResponse<IUserAuthData>> GetCurrent()
        {
            int code = 200;
            BaseResponse response = null;
            try
            {
                IUserAuthData authService = _authService.GetCurrentUser();
                if (_authService == null)
                {
                    code = 404;
                    response = new ErrorResponse("Not logged in as a user.");
                }
                else
                {
                    response = new ItemResponse<IUserAuthData>() { Item = authService };
                }
            }
            catch (Exception ex)
            {
                code = 500;
                response = new ErrorResponse(ex.Message);
                base.Logger.LogError(ex.ToString());
            }
            return StatusCode(code, response);
        }
        [AllowAnonymous]
        [HttpPost("register")]
        public ActionResult<ItemResponse<int>> Create(UserAddRequest model)
        {
            ObjectResult result = null;

            try
            {
                int id = _service.Add(model);
                ItemResponse<int> response = new ItemResponse<int>() { Item = id };
                result = Created201(response);
            }
            catch (Exception ex)
            {
                base.Logger.LogError(ex.ToString());
                ErrorResponse response = new ErrorResponse(ex.Message);

                result = StatusCode(500, response);
            }
            return result;
        }
        [HttpGet("{id:int}")]
        public ActionResult<ItemResponse<Users>> Get(int id)
        {
            int code = 200;
            BaseResponse response = null;
            try
            {
                Users user = _service.Get(id);

                if (user == null)
                {
                    code = 404;
                    response = new ErrorResponse("No Users with that Id");
                }
                else
                {
                    response = new ItemResponse<Users> { Item = user };
                }

            }
            catch (Exception ex)
            {
                code = 500;
                base.Logger.LogError(ex.ToString());
                response = new ErrorResponse($"Generic Errors: { ex.Message }");
            }
            return StatusCode(code, response);

        }
        [HttpGet("paginate")]
        public ActionResult<ItemResponse<Paged<Users>>> GetPage(int pageIndex, int pageSize)
        {
            int code = 200;
            BaseResponse response = null;

            try
            {
                Paged<Users> page = _service.Pagination(pageIndex, pageSize);

                if (page == null)
                {
                    code = 404;
                    response = new ErrorResponse("Error Message: No Users in range");
                }
                else
                {
                    response = new ItemResponse<Paged<Users>> { Item = page };
                }
            }
            catch (Exception ex)
            {
                code = 500;
                response = new ErrorResponse(ex.Message);
                base.Logger.LogError(ex.ToString());
            }

            return StatusCode(code, response);

        }
        [HttpPut("{id:int}")]
        public ActionResult<SuccessResponse> Update(UserUpdateRequest model)
        {
            int userId = _authService.GetCurrentUserId();
            int code = 200;
            BaseResponse response = null;

            try
            {
                _service.Update(model, userId);

                response = new SuccessResponse();
            }
            catch(Exception ex)
            {
                code = 500;
                response= new ErrorResponse(ex.Message);
            }
            return StatusCode(code, response);
        }
        [HttpPut("forgotpassword")]
        [AllowAnonymous]
        public ActionResult<SuccessResponse> Verify(ForgotPasswordRequest model)
        {
            bool isEmailValid = false;
            int code = 200;
            BaseResponse response = null;
            int passwordTokenType = (int)TokenType.ResetPassword;
            string email = model.Email.ToString();
            try
            {
                 isEmailValid = _service.VerifyEmail(email);

                if (!isEmailValid)
                {
                    code = 404;
                    response = new ErrorResponse("Email Does Not Exist");
                }
                else
                {   
                    string token = Guid.NewGuid().ToString("N");
                    bool tokenAdded = false;
                    tokenAdded = _service.AddToken(email, token, passwordTokenType);

                    if (tokenAdded)
                    {
                      
                        ChangePasswordEmailRequest request = new ChangePasswordEmailRequest();
                        request.Token = token;
                        request.Email = email;
                        _emailService.SendResetPasswordEmail(request);

                    }
                    response = new SuccessResponse();
                }
            }
            catch (Exception ex)
            {
                code = 500;
                response = new ErrorResponse(ex.Message);
                base.Logger.LogError(ex.ToString());
            }
            return StatusCode(code, response);
        }

        [HttpPut("changepassword")]
        [AllowAnonymous]
        public ActionResult<SuccessResponse> UpdatePassword(ChangePasswordRequest model)
        {
            int passwordTokenType = (int)TokenType.ResetPassword;
            int code = 200;
            BaseResponse response = null;
            try
            {
                _service.ChangePassword(model.Password, model.Token, passwordTokenType);
                response = new SuccessResponse();

            }
            catch (Exception ex)
            {
                code = 500;
                response = new ErrorResponse(ex.Message);
                base.Logger.LogError(ex.ToString());
            }
            return StatusCode(code, response);
        }


        [HttpDelete("{id:int}")]
        public ActionResult<SuccessResponse> Delete(int id)
        {
            int code = 200;
            BaseResponse response = null;

            try
            {
                _service.Delete(id);

                response = new SuccessResponse();
            }
            catch (Exception ex)
            {
                code = 500;
                response = new ErrorResponse(ex.Message);
                base.Logger.LogError(ex.ToString());

            }

            return StatusCode(code, response);
        }


        [HttpGet("statistics")]
        public ActionResult<ItemsResponse<RoleAnalytics>> GetLocationsStatistics(int id)
        {
            int code = 200;
            BaseResponse response = null;
            try
            {
                List<RoleAnalytics> usersList = _service.GetUserAnalytics();

                if (usersList == null)
                {
                    code = 404;
                    response = new ErrorResponse("Statistics not found");
                }
                else
                {
                    response = new ItemsResponse<RoleAnalytics> { Items = usersList };
                }

            }
            catch (Exception ex)
            {
                code = 500;
                base.Logger.LogError(ex.ToString());
                response = new ErrorResponse($"Generic Errors: {ex.Message}");
            }
            return StatusCode(code, response);

        }
    }
}
