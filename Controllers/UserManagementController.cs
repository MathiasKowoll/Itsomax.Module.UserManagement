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

        public UserManagementController(IManageUser manageUser,ICreateMenu createMenu,SignInManager<User> signIn,UserManager<User> user,
        IRepository<User> userRepository,RoleManager<Role> roleManage,IHttpContextAccessor httpContext)
        {
            _manageUser=manageUser;
            _createMenu = createMenu;
            _signIn = signIn;
            _user = user;
            _userRepository = userRepository;
            _roleManage = roleManage;
            _httpContext = httpContext;
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
                    
                    ViewBag.Message="User create succesfully";
                    ViewBag.Status="Success";
                    return RedirectToAction("ListActiveUsers");
                }
                else
                {
                    ViewBag.Message="User not created";
                    ViewBag.Status="Failed";
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
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }
                if (user.IsDeleted)
                {
                    return NotFound();
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
                    return Redirect("/Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
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
                    ViewBag.Message="User edited succesfully";
                    ViewBag.Status="Success";
                    return RedirectToAction("ListActiveUsers");
                }
                else
                {
                    ViewBag.Message="Failed editing user";
                    ViewBag.Status="Failed";
                    return RedirectToAction("ListActiveUsers");
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
                var currentUser = _user.GetUserAsync(_httpContext.HttpContext.User).Result;

                if (user != currentUser)
                {
                    try
					{
                        user.IsDeleted = true;
                        var res =_user.UpdateAsync(user).Result;
						//_userRepository.SaveChange();
						return Json(true);
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
        public async Task<IActionResult> LogOff()
        {
            await _signIn.SignOutAsync();
            return Redirect("/login");
        }

        public IActionResult ListActiveUsers()
        {
            return View();
        }

        public IActionResult ListDeletedUsers()
        {
            return View();
        }

        public IActionResult ListAllUsers()
        {
            return View();
        }

        [HttpGet]
        [Route("/get/all/active/users/json/")]
        public JsonResult ListActiveUsersJsonView()
        {
            var user = _user.Users.ToList().Where(x => x.IsDeleted == false).Select(x => new UserListViewModel
            {
                Id = x.Id,
                UserName = x.UserName,
                Email = x.Email,
                Updated = x.UpdatedOn.DateTime,
            });
                return Json(user);
              
        }

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

        public IActionResult ChangePasswordView()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ChangePasswordPostView(ChangePasswordViewModel model)
        {
            var currentUser = _user.GetUserAsync(_httpContext.HttpContext.User).Result;
            var res = _user.ChangePasswordAsync(currentUser, model.CurrentPassword, model.NewPassword).Result;
            if(res.Succeeded)
            {
                return Json(true);
            }
            else
            {
                return Json(false);
            }

        }

        public IActionResult ChangePasswordUserView()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ChangePasswordUserPostView()
        {
            return Json(true);
        }

        #region Helpers
        
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
        /* 
        private Task<User> GetCurrentUserAsync()
        {
            return _userManager.GetUserAsync(HttpContext.User);
        }
         */
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