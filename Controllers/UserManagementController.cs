using Microsoft.AspNetCore.Mvc;
using Itsomax.Module.UserManagement.Interfaces;
using Itsomax.Module.UserManagement.ViewModels;
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

namespace Itsomax.Module.UserManagement.Controllers
{
    [Authorize(Policy = "ManageAuthentification")]
    public class UserManagementController : Controller
    {
        private readonly IManageUser _manageUser;
        private readonly SignInManager<User> _signIn;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly ICreateMenu _createMenu;
        private readonly IRepository<User> _userRepository;
        private readonly IHttpContextAccessor _httpContext;
        private IToastNotification _toastNotification;
        private readonly ILogginToDatabase _logger;

        public UserManagementController(IManageUser manageUser,ICreateMenu createMenu,SignInManager<User> signIn,UserManager<User> user,
        IRepository<User> userRepository,RoleManager<Role> roleManage,IHttpContextAccessor httpContext, IToastNotification toastNotification,
        ILogginToDatabase logger)
        {
            _manageUser=manageUser;
            _createMenu = createMenu;
            _signIn = signIn;
            _userManager = user;
            _userRepository = userRepository;
            _roleManager = roleManage;
            _httpContext = httpContext;
            _toastNotification = toastNotification;
            _logger = logger;
        }

        public IActionResult CreateUser()
        {
            var userModel = new CreateUserViewModel();
            userModel.RolesList = _roleManager.Roles.ToList().Select(x => new SelectListItem()
            {
                Value = x.Name,
                Text = x.Name
            });
            return View(userModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUserPostView(CreateUserViewModel model,params string[] selectedRoles)
        {
            if (ModelState.IsValid)
            {
                var user = new User()
                {
                    Email = model.Email,
                    UserName = model.UserName
                };

                var resCreateUser = await _userManager.CreateAsync(user, model.Password);
                if (resCreateUser.Succeeded)
                {
                    var resAddRole = await _userManager.AddToRolesAsync(user, selectedRoles);
                    if (resAddRole.Succeeded)
                    {
                        var resClaimsAdd = _manageUser.CreateUserAddDefaultClaim(user.Id);
                        if (resClaimsAdd)
                        {
                            _manageUser.UpdateClaimValueForRole();
                            _toastNotification.AddToastMessage("User: " + model.UserName + " create succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
                            {
                                PositionClass = ToastPositions.TopCenter
                            });
                            return RedirectToAction("ListActiveUsers");
                        }
                    }
                    else
                    {
                        await _userManager.DeleteAsync(user);
                        _toastNotification.AddToastMessage("User: " + model.UserName + " not created", "Error: "+resAddRole.Errors, ToastEnums.ToastType.Error, new ToastOption()
                        {
                            PositionClass = ToastPositions.TopCenter
                        });
                        return View(model);
                    }
                }
                _toastNotification.AddToastMessage("User: " + model.UserName + " not created", "Error: "+resCreateUser.Errors, ToastEnums.ToastType.Error, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                return View(model);
            }
            else
                return View(model);

        }

        [HttpGet]
        [AllowAnonymous]
        [Route("login")]
        public IActionResult LoginView(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Route("login")]
        public async Task<IActionResult> LoginView(LoginUserViewModel model ,string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(model.UserName);
                if (user==null)
                {
                    _toastNotification.AddToastMessage("Invalid user or password, please try again", "", ToastEnums.ToastType.Warning, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("User does not exists or tried to enter null value for user.","Login",string.Empty,model.UserName);
                    return View(model);
                }
                if (user.IsDeleted)
                {
                    _toastNotification.AddToastMessage("Invalid user or password, please try again", "", ToastEnums.ToastType.Warning, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("User: "+user.UserName+" has been deleted","Login");
                    return View(model);
                }
                _manageUser.AddDefaultClaimAllUsers();
                _manageUser.UpdateClaimValueForRole();
                var res = await _signIn.PasswordSignInAsync(user,model.Password,model.RememberMe,true);
                if (res.Succeeded)
                {
                    _createMenu.CreteMenuFile();
                    _logger.InformationLog("User: "+user.UserName+" succesful login","Login",res.ToString(),user.UserName);
                    return RedirectToLocal(returnUrl);
                }
                if (res.IsLockedOut)
                {
                    _toastNotification.AddToastMessage("User is locked out, please contact your system administrator", "", ToastEnums.ToastType.Warning, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("User lockout","Login",res.ToString(),user.UserName);
                    return View(model);
                }
                else
                {
                    _toastNotification.AddToastMessage("Invalid user or password, please try again", "", ToastEnums.ToastType.Warning, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("User does not exists or tried to enter null value for user.","Login", string.Empty, model.UserName);
                    return View(model);
                }
            }
            return View(model);
        }
        [HttpGet("/get/user/{Id}")]
        public async Task<IActionResult> EditUserView(int? Id)
        {
            if (Id == null)
            {
                return NotFound();
            }
            var user = await _userManager.FindByIdAsync(Id.ToString());
            var locked = await _userManager.IsLockedOutAsync(user);
            var roles = _manageUser.GetUserRolesToSelectListItem(Id.Value);
            var EditUser = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                RolesList = roles,
                IsLocked = locked,
                IsDeleted = user.IsDeleted
            };
            return View(EditUser);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUserPostView(EditUserViewModel model, params string[] rolesAdd)
        {

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.Id.ToString());
                user.Email = model.Email;
                user.UserName = model.UserName;
                user.IsDeleted = model.IsDeleted;

                var res = await _userManager.UpdateAsync(user);
                if (res.Succeeded)
                {
                    var rolesRemove = await  _userManager.GetRolesAsync(user);
                    var resDel = await _userManager.RemoveFromRolesAsync(user, rolesRemove);
                    if (resDel.Succeeded)
                    {
                        var resAdd = await _userManager.AddToRolesAsync(user, rolesAdd);
                        if (resAdd.Succeeded)
                        {
                            if (model.IsLocked == true)
                            {
                                var resL = await _userManager.SetLockoutEndDateAsync(user, Convert.ToDateTime("3000-01-01"));
                                if(resL.Succeeded)
                                {
                                    _toastNotification.AddToastMessage("User: " + model.UserName + " modified succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
                                    {
                                        PositionClass = ToastPositions.TopCenter
                                    });
                                    _manageUser.CreateUserAddDefaultClaim(model.Id);
                                    _manageUser.UpdateClaimValueForRole();
                                    return RedirectToAction(nameof(ListActiveUsers));
                                }
                                else
                                {
                                    _toastNotification.AddToastMessage("Failed editing user " + model.UserName+ ", could not set lockout for user","Error: "+resL.Errors, ToastEnums.ToastType.Error, new ToastOption()
                                    {
                                        PositionClass = ToastPositions.TopCenter
                                    });
                                    return RedirectToAction(nameof(ListActiveUsers));
                                }
   
                            }
                            else
                            {

                                var resL = await _userManager.SetLockoutEndDateAsync(user, Convert.ToDateTime("1970-01-01"));
                                if (resL.Succeeded)
                                {
                                    _toastNotification.AddToastMessage("User: " + model.UserName + " modified succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
                                    {
                                        PositionClass = ToastPositions.TopCenter
                                    });
                                    _manageUser.CreateUserAddDefaultClaim(model.Id);
                                    _manageUser.UpdateClaimValueForRole();
                                    return RedirectToAction(nameof(ListActiveUsers));
                                }
                                else
                                {
                                    _toastNotification.AddToastMessage("Failed editing user " + model.UserName +", could not set lockout for user","Error: "+resL.Errors, ToastEnums.ToastType.Error, new ToastOption()
                                    {
                                        PositionClass = ToastPositions.TopCenter
                                    });
                                    return RedirectToAction(nameof(ListActiveUsers));
                                }
                            }

                        }
                        else
                        {
                            _toastNotification.AddToastMessage("Failed editing user " + model.UserName+ ", could not update roles","Error: "+resAdd.Errors, ToastEnums.ToastType.Error, new ToastOption()
                            {
                                PositionClass = ToastPositions.TopCenter
                            });
                            return RedirectToAction(nameof(ListActiveUsers));
                        }
                    }
                    else
                    {
                        _toastNotification.AddToastMessage("Failed editing user " + model.UserName+ ", could not update roles","Error: "+resDel.Errors, ToastEnums.ToastType.Error, new ToastOption()
                        {
                            PositionClass = ToastPositions.TopCenter
                        });
                        return RedirectToAction(nameof(ListActiveUsers));
                    }
                }
                else
                {
                    _toastNotification.AddToastMessage("Failed editing user " + model.UserName + ", could not update roles", "Error: " + res.Errors, ToastEnums.ToastType.Success, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _manageUser.CreateUserAddDefaultClaim(model.Id);
                    _manageUser.UpdateClaimValueForRole();
                    return RedirectToAction(nameof(ListActiveUsers));
                }
            }
            else
            {
                return View(model);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteUserPostView(long? Id)
        {
            if(Id == null)
            {
                return Json(false);
            }
            else
            {
                var user = await _userManager.FindByIdAsync(Id.Value.ToString());
                var currentUser = GetCurrentUserAsync().Result;

                if (user != currentUser)
                {
                    try
                    {
                        if(user.IsDeleted == true)
                        {
                            user.IsDeleted = false;
                            var res = await _userManager.UpdateAsync(user);
                            if (res.Succeeded)
                            {
                                return Json(true);

                            }
                            else
                            {
                                return Json(false);
                            }

                        }
                        else
                        {
                            user.IsDeleted = true;
                            var res = await _userManager.UpdateAsync(user);
                            if (res.Succeeded)
                            {
                                return Json(true);

                            }
                            else
                            {
                                return Json(false);
                            }
                        }
                        
                    }

                    catch (Exception ex)
                    {
                        var Message = ex.Message;
                        return Json(false);
                    }
                }
                else
                {
                    return Json(false);
                }


            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeletePermanentlyUserPostView(long? Id)
        {
            if (Id == null)
            {
                return Json(false);
            }
            else
            {
                var user = await _userManager.FindByIdAsync(Id.ToString());
                if(user.IsDeleted == false)
                {
                    return Json(false);
                }
                var res = await _userManager.DeleteAsync(user);
                if(res.Succeeded)
                {
                    return Json(true);
                }
                else
                {
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
                _toastNotification.AddToastMessage("An error ocurred: "+ex, "", ToastEnums.ToastType.Error, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                //_logger.LogError(LogginEvents);
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
                _toastNotification.AddToastMessage("Your password has been changed succesfully", "",ToastEnums.ToastType.Success,new ToastOption()
                {
                    PositionClass=ToastPositions.TopCenter
                });
                return RedirectPermanent("/Admin/WelcomePage");
            }
            else
            {
                _toastNotification.AddToastMessage("There was a problem changing your password", "error: "+res.Errors, ToastEnums.ToastType.Error,new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                return RedirectPermanent("/Admin/WelcomePage");
            }

        }
        
        public IActionResult ChangePasswordUserView(long? Id)
        {
            if (Id == null)
                return NotFound();

            var user = _userManager.FindByIdAsync(Id.ToString()).Result;
            var userChange = new ChangePasswordUserViewModel
            {
                UserId = Id.Value,
                UserName = user.UserName,
                NewPassword = "xxxxxxxxx",
                ConfirmPassword = "xxxxxxxxx"
            };
            return View(userChange);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePasswordUserPostView(ChangePasswordUserViewModel model)
        {
            if(model.NewPassword == "xxxxxxxxx")
            {
                _toastNotification.AddToastMessage("Your password has not been changed, please try a new one", "", ToastEnums.ToastType.Warning, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                return Json(true);
            }
            var user = _userManager.FindByIdAsync(model.UserId.ToString()).Result;
            var token = _userManager.GeneratePasswordResetTokenAsync(user).Result;
            var res = _userManager.ResetPasswordAsync(user,token,model.NewPassword).Result;
            if (res.Succeeded)
            {
                _toastNotification.AddToastMessage("Password for user " + user.UserName + " changed succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                return RedirectToAction(nameof(ListActiveUsers));
            }
            else
            {
                _toastNotification.AddToastMessage("Could not change password, please try again", "Error: "+res.Errors, ToastEnums.ToastType.Error, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                return View(model);
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