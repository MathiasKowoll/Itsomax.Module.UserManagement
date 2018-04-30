using Microsoft.AspNetCore.Mvc;
using Itsomax.Module.UserCore.Interfaces;
using Itsomax.Module.UserCore.ViewModels;
using Itsomax.Module.Core.Interfaces;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Itsomax.Module.Core.Models;
using Itsomax.Data.Infrastructure.Data;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using Microsoft.AspNetCore.Http;
using NToastNotify;
//using NToastNotify.Libraries;

namespace Itsomax.Module.UserManagement.Controllers
{
    [Authorize(Policy = "ManageAuthentification")]
    public class UserManagementController : Controller
    {
        private readonly IManageUser _manageUser;
        private readonly SignInManager<User> _signIn;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        //private readonly ICreateMenu _createMenu;
        //private readonly IRepository<AppSetting> _appSettings;
        private readonly IToastNotification _toastNotification;
        private readonly ILogginToDatabase _logger;

        public UserManagementController(IManageUser manageUser,ICreateMenu createMenu,SignInManager<User> signIn,UserManager<User> user,
        RoleManager<Role> roleManage, IToastNotification toastNotification,
        ILogginToDatabase logger, IRepository<AppSetting> appSettings)
        {
            _manageUser=manageUser;
            //_createMenu = createMenu;
            _signIn = signIn;
            _userManager = user;
            _roleManager = roleManage;
            _toastNotification = toastNotification;
            _logger = logger;
            //_appSettings = appSettings;
        }
        //[CheckSessionOut]
        public IActionResult CreateUser()
        {
            var userModel = new CreateUserViewModel
            {
                RolesList = _roleManager.Roles.ToList().Select(x => new SelectListItem()
                {
                    Value = x.Name,
                    Text = x.Name
                })
            };
            return View(userModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUserPostView(CreateUserViewModel model,params string[] selectedRoles)
        {
            if (ModelState.IsValid)
            {

                var res = await _manageUser.CreateUserAsync(model, selectedRoles);
                if (res.Succeeded)
                {
                    _toastNotification.AddSuccessToastMessage("User: " + model.UserName + " create succesfully", new ToastrOptions()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("User "+model.UserName+" has been created succesfully", "Create user", string.Empty, GetCurrentUserAsync().Result.UserName);
                    return RedirectToAction("ListActiveUsers");
                }
                else
                {
                    _logger.InformationLog(res.Errors, "Create user", "", GetCurrentUserAsync().Result.UserName);
                    _toastNotification.AddErrorToastMessage(res.Errors, new ToastrOptions()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    return View(nameof(CreateUser),model);
                }
            }
            else
                return View(nameof(CreateUser),model);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("Login")]
        public IActionResult LoginView(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Route("Login")]
        public async Task<IActionResult> LoginView(LoginUserViewModel model ,string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);
            var res = await _manageUser.UserLoginAsync(model);
            if (res.Succeeded)
            {
                HttpContext.Session.SetString("SessionId",model.UserName.ToUpper());
                _logger.InformationLog("User: "+model.UserName+" succesful login","Login",res.ToString(),model.UserName);
                return RedirectToLocal(returnUrl);
            }
            else
            {
                _toastNotification.AddWarningToastMessage("Invalid user or password, please try again", new ToastrOptions()
                {
                    PositionClass = ToastPositions.TopCenter,
                    
                });
                _logger.InformationLog(res.Errors,"Login",string.Empty,model.UserName);
                return View(model);
            }
        }
        [HttpGet("/get/user/{Id}")]
        public async Task<IActionResult> EditUserView(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var user = await _userManager.FindByIdAsync(id.ToString());
            if(user == null)
            {
                return NotFound();
            }
            var locked = await _userManager.IsLockedOutAsync(user);
            var roles = _manageUser.GetUserRolesToSelectListItem(id.Value);
            var editUser = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                RolesList = roles,
                IsLocked = locked,
                IsDeleted = user.IsDeleted
            };
            return View(editUser);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUserPostView(EditUserViewModel model, params string[] rolesAdd)
        {
            var roles = _manageUser.GetUserRolesToSelectListItem(model.Id);
            if (ModelState.IsValid)
            {
                var res = await _manageUser.EditUserAsync(model, rolesAdd);
                if (!res.Succeeded)
                {
                    _toastNotification.AddErrorToastMessage(res.Errors, new ToastrOptions()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog(res.Errors, "Edit User", string.Empty,GetCurrentUserAsync().Result.UserName);
                    model.RolesList = roles;
                    return View(nameof(EditUserView), model);
                }
                else
                {
                    _toastNotification.AddSuccessToastMessage("User: " + model.UserName + " modified succesfully", new ToastrOptions()
                    {
                        PositionClass = ToastPositions.TopCenter,
                        PreventDuplicates = true
                    });
                    _logger.InformationLog("User " + model.UserName + " modified succesfully", "Edit User", string.Empty, GetCurrentUserAsync().Result.UserName);
                    return RedirectToAction(nameof(ListActiveUsers));
                }
            }
            else
            {
                model.RolesList = roles;
                return View(nameof(EditUserView), model);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteUserPostView(long? id)
        {
            if(id == null)
            {
                return Json(false);
            }
            else
            {
                var user = await _userManager.FindByIdAsync(id.Value.ToString());

                if(user == null)
                {
                    return Json(false);
                }

                var currentUser = GetCurrentUserAsync().Result;

                if (user != currentUser)
                {
                    try
                    {
                        if(user.IsDeleted)
                        {
                            user.IsDeleted = false;
                            var res = await _userManager.UpdateAsync(user);
                            if (res.Succeeded)
                            {
                                _logger.InformationLog("User " + user.UserName + " disabled succesful", "Disable User",string.Empty, GetCurrentUserAsync().Result.UserName);
                                return Json(true);

                            }
                            else
                            {
                                _logger.InformationLog("User " + user.UserName + " not disabled succesful", "Disable User", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                                return Json(false);
                            }

                        }
                        else
                        {
                            user.IsDeleted = true;
                            var res = await _userManager.UpdateAsync(user);
                            if (res.Succeeded)
                            {
                                _logger.InformationLog("User " + user.UserName + " enabled succesful", "Disable User", string.Empty, GetCurrentUserAsync().Result.UserName);
                                return Json(true);

                            }
                            else
                            {
                                _logger.InformationLog("User " + user.UserName + " not enabled succesful", "Disable User", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                                return Json(false);
                            }
                        }
                        
                    }

                    catch (Exception ex)
                    {
                        var message = ex.Message;
                        _logger.ErrorLog(ex.Message, "Disable User", ex.InnerException.Message, GetCurrentUserAsync().Result.UserName);
                        return Json(false);
                    }
                }
                else
                {
                    _logger.InformationLog("User " + user.UserName + " tried to disable himself", "Disable User", string.Empty, GetCurrentUserAsync().Result.UserName);
                    return Json(false);
                }


            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeletePermanentlyUserPostView(long? id)
        {
            if (id == null)
            {
                _logger.InformationLog("Id is null", "Delete User", "", GetCurrentUserAsync().Result.UserName);
                return Json(false);
            }
            else
            {
                var user = await _userManager.FindByIdAsync(id.ToString());
                if(user == null)
                {
                    _logger.InformationLog("User with "+id+" not found", "Delete User", "", GetCurrentUserAsync().Result.UserName);
                    return Json(false);
                }

                try
                {
                    if (user.IsDeleted == false)
                    {
                        return Json(false);
                    }
                    var res = await _userManager.DeleteAsync(user);
                    if (res.Succeeded)
                    {
                        _logger.InformationLog("User"+user.UserName+" deleted succesfully", "Delete User", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                        return Json(true);
                    }
                    else
                    {
                        _logger.InformationLog("User " + user.UserName + " not deleted succesfully", "Delete User", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                        return Json(false);
                    }
                }
                catch(Exception ex)
                {
                    _logger.ErrorLog(ex.Message, "Delete User", ex.InnerException.Message, GetCurrentUserAsync().Result.UserName);
                    return Json(false);
                }
                
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> LogOff()
        {
            await _signIn.SignOutAsync();
            return Redirect("/login");
        }

        public IActionResult ListActiveUsers()
        {
            var user = GetCurrentUserAsync().Result;
            ViewBag.UserId = user.Id;
           return View();
        }

        [HttpGet]
        [Route("/get/all/active/users/json/")]
        public JsonResult ListActiveUsersJsonView()
        {
            try
            {
                var user = _userManager.Users.ToList().Select(x => new UserListViewModel
                {
                    Id = x.Id,
                    UserName = x.UserName,
                    Email = x.Email,
                    IsDeleted = x.IsDeleted,
                    Updated = x.UpdatedOn.DateTime,
                });
                return Json(user);
            }
            catch(Exception ex)
            {
                _toastNotification.AddErrorToastMessage("An error ocurred: "+ex.Message, new ToastrOptions()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                _logger.ErrorLog(ex.Message, "Active user list", ex.InnerException.Message, GetCurrentUserAsync().Result.UserName);
                return Json(false);
            }
            
              
        }

        public IActionResult ChangePasswordView()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePasswordPostView(ChangePasswordViewModel model)
        {
            var currentUser = GetCurrentUserAsync().Result;
            var res = _userManager.ChangePasswordAsync(currentUser, model.CurrentPassword, model.NewPassword).Result;
            if(res.Succeeded)
            {
                _toastNotification.AddSuccessToastMessage("Your password has been changed succesfully",new ToastrOptions()
                {
                    PositionClass=ToastPositions.TopCenter
                });
                _logger.InformationLog("Own password change succesfully", "Own password change");
                return RedirectPermanent("/Admin/WelcomePage");
            }
            else
            {
                _toastNotification.AddErrorToastMessage("There was a problem changing your password",new ToastrOptions()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                _logger.InformationLog("Own password change succesfully", "Own password not change", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                ModelState.AddModelError(nameof(ChangePasswordViewModel.NewPassword), AddErrorList(res));
                return View(nameof(ChangePasswordView),model);
            }

        }
        
        public IActionResult ChangePasswordUserView(long? id)
        {
            if (id == null)
                return NotFound();

            var user = _userManager.FindByIdAsync(id.ToString()).Result;
            if (user == null)
                return NotFound();

            var userChange = new ChangePasswordUserViewModel
            {
                UserId = id.Value,
                UserName = user.UserName,
                NewPassword = string.Empty,
                ConfirmPassword = string.Empty
            };
            return View(userChange);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordUserPostView(ChangePasswordUserViewModel model)
        {
            if(model.NewPassword == string.Empty)
            {
                _toastNotification.AddWarningToastMessage("Your password has not been changed, please try a new one", new ToastrOptions()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                return Json(false);
            }
            var user = await _userManager.FindByIdAsync(model.UserId.ToString());
            if(user == null)
            {
                return NotFound();
            }
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var res =  await _userManager.ResetPasswordAsync(user,token,model.NewPassword);
            if (res.Succeeded)
            {
                _toastNotification.AddSuccessToastMessage("Password for user " + user.UserName + " changed succesfully", new ToastrOptions()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                _logger.InformationLog("User " + user.UserName + " password changed succesfully", "Password Change", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                return RedirectToAction(nameof(ListActiveUsers));
            }
            else
            {
                _logger.InformationLog("User "+user.UserName+" password not changed succesfully", "Password Change", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                ModelState.AddModelError(nameof(ChangePasswordViewModel.NewPassword),AddErrorList(res));
                return View("ChangePasswordUserView", model);
            }
                
        }

        #region Helpers
        
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private string AddErrorList(IdentityResult result)
        {
            var errorList = string.Empty;
            foreach (var error in result.Errors)
            {
                errorList = errorList + " " + error.Description;
            }
            return errorList;
        }
        
        private Task<User> GetCurrentUserAsync()
        {
            return _userManager.GetUserAsync(HttpContext.User);
        }
         
        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return Redirect("/");
            }
        }

        #endregion
    }
}