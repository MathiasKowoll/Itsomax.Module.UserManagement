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
        private readonly IRepository<AppSetting> _appSettings;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IToastNotification _toastNotification;
        private readonly ILogginToDatabase _logger;

        public UserManagementController(IManageUser manageUser,ICreateMenu createMenu,SignInManager<User> signIn,UserManager<User> user,
        IRepository<User> userRepository,RoleManager<Role> roleManage,IHttpContextAccessor httpContext, IToastNotification toastNotification,
        ILogginToDatabase logger, IRepository<AppSetting> appSettings)
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
            _appSettings = appSettings;
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
                            _logger.InformationLog("User "+user.UserName+" has been created succesfully", "Create user", string.Empty, GetCurrentUserAsync().Result.UserName);
                            return RedirectToAction("ListActiveUsers");
                        }
                    }
                    else
                    {
                        await _userManager.DeleteAsync(user);
                        _logger.InformationLog("Error while creating user " + model.UserName, "Create user", AddErrorList(resAddRole), GetCurrentUserAsync().Result.UserName);
                        _toastNotification.AddToastMessage("User: " + model.UserName + " not created", "", ToastEnums.ToastType.Error, new ToastOption()
                        {
                            PositionClass = ToastPositions.TopCenter
                        });
                        return View(model);
                    }
                }
                _logger.InformationLog("Error while creating user " + model.UserName, "Create user", AddErrorList(resCreateUser), GetCurrentUserAsync().Result.UserName);
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
            if (!ModelState.IsValid) return View(model);
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
                if(_appSettings.Query().Any(x => x.Key== "NewModuleCreateMenu" && x.Value=="true"))
                {
                    _createMenu.CreteMenuFile();
                }
                _logger.InformationLog("User: "+user.UserName+" succesful login","Login",res.ToString(),user.UserName);
                return RedirectToLocal(returnUrl);
            }
            if (res.IsLockedOut)
            {
                _toastNotification.AddToastMessage("User is locked out, please contact your system administrator", "", ToastEnums.ToastType.Warning, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                _logger.InformationLog("User " + user.UserName + " lockout", "Login",string.Empty,user.UserName);
                return View(model);
            }
            _toastNotification.AddToastMessage("Invalid user or password, please try again", "", ToastEnums.ToastType.Warning, new ToastOption()
            {
                PositionClass = ToastPositions.TopCenter
            });
            _logger.InformationLog("User " + user.UserName + " does not exists or tried to enter null value for user.", "Login", string.Empty, model.UserName);
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
            if(user == null)
            {
                return NotFound();
            }
            var locked = await _userManager.IsLockedOutAsync(user);
            var roles = _manageUser.GetUserRolesToSelectListItem(Id.Value);
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

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.Id.ToString());
                if(user == null)
                {
                    _toastNotification.AddToastMessage("The user: " + model.UserName + " not found", "", ToastEnums.ToastType.Error, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("User " + model.UserName + " not found", "Edit User", string.Empty,GetCurrentUserAsync().Result.UserName);
                    return View(nameof(EditUserView), model);
                }
                if(user.Id != model.Id)
                {
                    _toastNotification.AddToastMessage("The user: " + model.UserName + " not found", "", ToastEnums.ToastType.Error, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("User " + model.UserName + " not found", "Edit User", "The user Id do not correspond between model and user", GetCurrentUserAsync().Result.UserName);
                    return View(nameof(EditUserView), model);
                }
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
                            if (model.IsLocked)
                            {
                                var resL = await _userManager.SetLockoutEndDateAsync(user, Convert.ToDateTime("3000-01-01"));
                                if(resL.Succeeded)
                                {
                                    _toastNotification.AddToastMessage("User: " + model.UserName + " modified succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
                                    {
                                        PositionClass = ToastPositions.TopCenter,
                                        PreventDuplicates = true
                                    });
                                    _logger.InformationLog("User " + user.UserName + " modified succesfully", "Edit User", string.Empty, GetCurrentUserAsync().Result.UserName);
                                    _manageUser.CreateUserAddDefaultClaim(model.Id);
                                    _manageUser.UpdateClaimValueForRole();
                                    return RedirectToAction(nameof(ListActiveUsers));
                                }
                                else
                                {
                                    _toastNotification.AddToastMessage("Failed editing user " + model.UserName+ ", could not set lockout for user","", ToastEnums.ToastType.Error, new ToastOption()
                                    {
                                        PositionClass = ToastPositions.TopCenter
                                    });
                                    ModelState.AddModelError(nameof(EditUserViewModel.IsLocked), AddErrorList(resL));
                                    _logger.InformationLog("Failed editing user " + model.UserName + ", could not set lockout for user", "Edit User", AddErrorList(resL), GetCurrentUserAsync().Result.UserName);
                                    return View(nameof(EditUserView),model);
                                }
   
                            }
                            else
                            {

                                var resL = await _userManager.SetLockoutEndDateAsync(user, Convert.ToDateTime("1970-01-01"));
                                if (resL.Succeeded)
                                {
                                    _toastNotification.AddToastMessage("User: " + model.UserName + " modified succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
                                    {
                                        PositionClass = ToastPositions.TopCenter,
                                        PreventDuplicates = true
                                    });
                                    _logger.InformationLog("User " + user.UserName + " modified succesfully", "Edit User", "", GetCurrentUserAsync().Result.UserName);
                                    _manageUser.CreateUserAddDefaultClaim(model.Id);
                                    _manageUser.UpdateClaimValueForRole();
                                    return RedirectToAction(nameof(ListActiveUsers));
                                }
                                else
                                {
                                    _toastNotification.AddToastMessage("Failed editing user " + model.UserName +", could not set lockout for user","", ToastEnums.ToastType.Error, new ToastOption()
                                    {
                                        PositionClass = ToastPositions.TopCenter
                                    });
                                    _logger.InformationLog("Failed editing user " + model.UserName + ", could not set lockout for user", "Edit User", AddErrorList(resL), GetCurrentUserAsync().Result.UserName);
                                    return View(nameof(EditUserView), model);
                                }
                            }

                        }
                        else
                        {
                            _toastNotification.AddToastMessage("Failed editing user " + model.UserName+ ", could not update roles","", ToastEnums.ToastType.Error, new ToastOption()
                            {
                                PositionClass = ToastPositions.TopCenter
                            });
                            _logger.InformationLog("Failed editing user " + model.UserName + ", could not update roles", "Edit User", AddErrorList(resAdd), GetCurrentUserAsync().Result.UserName);
                            return View(nameof(EditUserView), model);
                        }
                    }
                    else
                    {
                        _toastNotification.AddToastMessage("Failed editing user " + model.UserName+ ", could not update roles","", ToastEnums.ToastType.Error, new ToastOption()
                        {
                            PositionClass = ToastPositions.TopCenter
                        });
                        _logger.InformationLog("Failed editing user " + model.UserName + ", could not update roles", "Edit User", AddErrorList(resDel), GetCurrentUserAsync().Result.UserName);
                        return View(nameof(EditUserView), model);
                    }
                }
                else
                {
                    _toastNotification.AddToastMessage("Failed editing user " + model.UserName + ", could not update roles", " ", ToastEnums.ToastType.Success, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("Failed editing user " + model.UserName + ", could not update roles", "Edit User", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                    _manageUser.CreateUserAddDefaultClaim(model.Id);
                    _manageUser.UpdateClaimValueForRole();
                    return View(nameof(EditUserView), model);
                }
            }
            else
            {
                return View(nameof(EditUserView), model);
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

                if(user == null)
                {
                    return Json(false);
                }

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
                        var Message = ex.Message;
                        _logger.ErrorLog(ex.Message, "Disable User", ex.InnerException.ToString(), GetCurrentUserAsync().Result.UserName);
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
        public async Task<IActionResult> DeletePermanentlyUserPostView(long? Id)
        {
            if (Id == null)
            {
                _logger.InformationLog("Id is null", "Delete User", "", GetCurrentUserAsync().Result.UserName);
                return Json(false);
            }
            else
            {
                var user = await _userManager.FindByIdAsync(Id.ToString());
                if(user == null)
                {
                    _logger.InformationLog("User with "+Id+" not found", "Delete User", "", GetCurrentUserAsync().Result.UserName);
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
                    _logger.ErrorLog(ex.Message, "Delete User", ex.InnerException.ToString(), GetCurrentUserAsync().Result.UserName);
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
                _toastNotification.AddToastMessage("An error ocurred: "+ex.Message, "", ToastEnums.ToastType.Error, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                _logger.ErrorLog(ex.Message, "Active user list", ex.InnerException.ToString(), GetCurrentUserAsync().Result.UserName);
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
                _logger.InformationLog("Own password change succesfully", "Own password change");
                return RedirectPermanent("/Admin/WelcomePage");
            }
            else
            {
                _toastNotification.AddToastMessage("There was a problem changing your password", "", ToastEnums.ToastType.Error,new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                _logger.InformationLog("Own password change succesfully", "Own password not change", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                ModelState.AddModelError(nameof(ChangePasswordViewModel.NewPassword), AddErrorList(res));
                return View(nameof(ChangePasswordView),model);
            }

        }
        
        public IActionResult ChangePasswordUserView(long? Id)
        {
            if (Id == null)
                return NotFound();

            var user = _userManager.FindByIdAsync(Id.ToString()).Result;
            if (user == null)
                return NotFound();

            var userChange = new ChangePasswordUserViewModel
            {
                UserId = Id.Value,
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
                _toastNotification.AddToastMessage("Your password has not been changed, please try a new one", "", ToastEnums.ToastType.Warning, new ToastOption()
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
                _toastNotification.AddToastMessage("Password for user " + user.UserName + " changed succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
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