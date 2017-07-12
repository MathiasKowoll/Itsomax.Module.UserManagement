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
        private readonly UserManager<User> _user;
        private readonly RoleManager<Role> _roleManage;
        private readonly ICreateMenu _createMenu;
        private readonly IRepository<User> _userRepository;
        private readonly IHttpContextAccessor _httpContext;
        private IToastNotification _toastNotification;

        public UserManagementController(IManageUser manageUser,ICreateMenu createMenu,SignInManager<User> signIn,UserManager<User> user,
        IRepository<User> userRepository,RoleManager<Role> roleManage,IHttpContextAccessor httpContext, IToastNotification toastNotification)
        {
            _manageUser=manageUser;
            _createMenu = createMenu;
            _signIn = signIn;
            _user = user;
            _userRepository = userRepository;
            _roleManage = roleManage;
            _httpContext = httpContext;
            _toastNotification = toastNotification;
        }

        public IActionResult CreateUser()
        {
            var userModel = new CreateUserViewModel();
            userModel.RolesList = _roleManage.Roles.ToList().Select(x => new SelectListItem()
            {
                Value = x.Name,
                Text = x.Name
            });
            return View(userModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateUserPostView(CreateUserViewModel model,params string[] selectedRoles)
        {
            if (ModelState.IsValid)
            {
                var res = _manageUser.CreateUser(model, selectedRoles).Result;
                if(res.Succeeded)
                {
                    _toastNotification.AddToastMessage("User: " + model.UserName + " create succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    return RedirectToAction("ListActiveUsers");
                }
                else
                {
                    _toastNotification.AddToastMessage("User: " + model.UserName + " not created", "", ToastEnums.ToastType.Error, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    ViewBag.Message="User: "+model.UserName+" not created";
                    ViewBag.Success = 1;
                    return View(model);
                }
                
                
            }
            else
                return View(model);

        }

        [HttpGet]
        [AllowAnonymous]
        [Route("login")]
        public IActionResult LoginView(string returnUrl = null)
        {
            _manageUser.CreateAdminfirstFirsRun();
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
                var user = _user.FindByNameAsync(model.UserName).Result;
                if (user==null)
                {
                    _toastNotification.AddToastMessage("Invalid user or password, please try again", "", ToastEnums.ToastType.Warning, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    return View(model);
                }
                if (user.IsDeleted)
                {
                    _toastNotification.AddToastMessage("Invalid user or password, please try again", "", ToastEnums.ToastType.Warning, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    return View(model);
                }
                _manageUser.CreateUserAddDefaultClaim(user.Id);
                _manageUser.UpdateClaimValueForRole();
                var res = await _signIn.PasswordSignInAsync(user,model.Password,model.RememberMe,true);
                if (res.Succeeded)
                {
                    _createMenu.CreteMenuFile();
                    return RedirectToLocal(returnUrl);
                }
                if (res.IsLockedOut)
                {
                    _toastNotification.AddToastMessage("User is locked out, please contact your system administrator", "", ToastEnums.ToastType.Warning, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    return View(model);
                }
                else
                {
                    _toastNotification.AddToastMessage("Invalid user or password, please try again", "", ToastEnums.ToastType.Warning, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    return View(model);
                }
            }
            return View(model);
        }
        [HttpGet("/get/user/{Id}")]
        public IActionResult EditUserView(int? Id)
        {
            if (Id == null)
            {
                return NotFound();
            }
            var user = _user.FindByIdAsync(Id.ToString()).Result;
            var locked = _user.IsLockedOutAsync(user).Result;
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
        public IActionResult EditUserPostView(EditUserViewModel model, params string[] selectedRoles)
        {

            if (ModelState.IsValid)
            {
                var res = _manageUser.EditUser(model, selectedRoles).Result;
                if (res.Succeeded)
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
                    _toastNotification.AddToastMessage("Failed editing user " + model.UserName, "", ToastEnums.ToastType.Error, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    return RedirectToAction(nameof(ListActiveUsers));
                }
                    
            }
            else
                return View(model);

        }

        [HttpDelete]
        public IActionResult DeleteUserPostView(long? Id)
        {
            if(Id == null)
            {
                return Json(false);
            }
            else
            {
                var user = _user.FindByIdAsync(Id.Value.ToString()).Result;
                var currentUser = GetCurrentUserAsync().Result;

                if (user != currentUser)
                {
                    try
                    {
                        if(user.IsDeleted == true)
                        {
                            user.IsDeleted = false;
                            var res = _user.UpdateAsync(user).Result;
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
                            var res = _user.UpdateAsync(user).Result;
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
        public IActionResult DeletePermanentlyUserPostView(long? Id)
        {
            if (Id == null)
            {
                return Json(false);
            }
            else
            {
                var user = _user.FindByIdAsync(Id.ToString()).Result;
                if(user.IsDeleted == false)
                {
                    return Json(false);
                }
                var res = _user.DeleteAsync(user).Result;
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
        /*
        public IActionResult ListDeletedUsers()
        {
            return View();
        }

        public IActionResult ListAllUsers()
        {
            return View();
        }
        */
        [HttpGet]
        [Route("/get/all/active/users/json/")]
        public JsonResult ListActiveUsersJsonView()
        {
            var user = _user.Users.ToList().Select(x => new UserListViewModel
            {
                Id = x.Id,
                UserName = x.UserName,
                Email = x.Email,
                IsDeleted = x.IsDeleted,
                Updated = x.UpdatedOn.DateTime,
            });
            return Json(user);
              
        }
        /*
        [HttpGet]
        [Route("/get/all/deleted/users/json/")]
        public JsonResult ListDeletedUsersJsonView()
        {
            var user = _user.Users.ToList().Where(x => x.IsDeleted == true).Select(x => new UserListViewModel
            {
                Id = x.Id,
                UserName = x.UserName,
                Email = x.Email,
                Updated = x.UpdatedOn.DateTime,
                //IsDeleted = x.IsDeleted

            });
            return Json(user);

        }
        
        [HttpGet]
        [Route("/get/all/users/json/")]
        public JsonResult ListAllUsersJsonView()
        {
            var user = _user.Users.ToList().Select(x => new UserListViewModel
            {
                Id = x.Id,
                UserName = x.UserName,
                Email = x.Email,
                Updated = x.UpdatedOn.DateTime,
                IsDeleted = x.IsDeleted

            });
            return Json(user);

        }
        */
        public IActionResult ChangePasswordView()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePasswordPostView(ChangePasswordViewModel model)
        {
            var currentUser = GetCurrentUserAsync().Result;
            var res = _user.ChangePasswordAsync(currentUser, model.CurrentPassword, model.NewPassword).Result;
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
                _toastNotification.AddToastMessage("There was a problem changing your password", "", ToastEnums.ToastType.Error,new ToastOption()
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

            var user = _user.FindByIdAsync(Id.ToString()).Result;
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
                ViewBag.Message = "Password not modified";
                ViewBag.Success = 1;
                return Json(true);
            }
            var user = _user.FindByIdAsync(model.UserId.ToString()).Result;
            var token = _user.GeneratePasswordResetTokenAsync(user).Result;
            var res = _user.ResetPasswordAsync(user,token,model.NewPassword).Result;
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
                _toastNotification.AddToastMessage("Could not change password, please try again", "", ToastEnums.ToastType.Error, new ToastOption()
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
            return _user.GetUserAsync(HttpContext.User);
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