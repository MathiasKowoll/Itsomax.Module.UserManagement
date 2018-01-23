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
using Itsomax.Module.Core.Extensions.CommonHelpers;
using NToastNotify;
using System;

namespace Itsomax.Module.UserManagement.Controllers
{
    [Authorize(Policy = "ManageAuthentification")]
    public class RoleManagementController : Controller
    {
        private readonly RoleManager<Role> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly IRepository<ModuleRole> _modRoleRepository;
        private readonly IRepository<SubModule> _subModule;
        private readonly IRepository<Role> _role;
        private readonly IManageUser _manageUser;
        private IToastNotification _toastNotification;
        private readonly ILogginToDatabase _logger;


        public RoleManagementController(RoleManager<Role> roleManager,IRepository<ModuleRole> modRoleRepository,
                                    IRepository<SubModule> subModule,IManageUser manageUser, IRepository<Role> role,
                                    IToastNotification toastNotification, ILogginToDatabase logger, UserManager<User> userManager)
        {
            _roleManager = roleManager;
            _modRoleRepository = modRoleRepository;
            _subModule = subModule;
            _manageUser = manageUser;
            _role = role;
            _toastNotification = toastNotification;
            _logger = logger;
            _userManager = userManager;
        }

        public IActionResult CreateRole()
        {
            var modulelist = new CreateRoleViewModel();
            try
            {
                
                modulelist.ModuleList = _subModule.Query().Select(x => new SelectListItem
                {
                    Value = x.Name,
                    Text = StringHelperClass.CamelSplit(x.Name)
                });
                return View(modulelist);
            }
            catch(Exception ex)
            {
                _toastNotification.AddToastMessage("Error ocurrerd: "+ex, "", ToastEnums.ToastType.Success, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                _logger.ErrorLog(ex.Message, "Create Role", ex.InnerException.Message, GetCurrentUserAsync().Result.UserName);
                return RedirectToAction("ListRoles");
            }
            
            
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateRolePostView(CreateRoleViewModel model, params string[] selectedModules)
        {
            Role role = new Role
            {
                Name = model.RoleName
            };

            if(ModelState.IsValid)
            {
                var res = _roleManager.CreateAsync(role).Result;
                if (res.Succeeded)
                {
                    try
                    {
                        foreach (var item in selectedModules)
                        {
                            var mod = _subModule.Query().FirstOrDefault(x => x.Name.Contains(item));
                            ModuleRole modrole = new ModuleRole
                            {
                                RoleId = role.Id,
                                SubModuleId = mod.Id
                            };
                            _modRoleRepository.Add(modrole);
                            _modRoleRepository.SaveChange();
                        }
                        _manageUser.UpdateClaimValueForRole();
                        _toastNotification.AddToastMessage("Role: " + model.RoleName + " created succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
                        {
                            PositionClass = ToastPositions.TopCenter
                        });
                        _logger.InformationLog("Role" + role.Name + " created succesfully", "Create Role", string.Empty, GetCurrentUserAsync().Result.UserName);
                        return RedirectToAction("ListRoles");
                    }
                    catch (Exception ex)
                    {
                        _toastNotification.AddToastMessage("Could not create role: " + model.RoleName, "", ToastEnums.ToastType.Error, new ToastOption()
                        {
                            PositionClass = ToastPositions.TopCenter
                        });
                        _logger.InformationLog(ex.Message, "Create Role", ex.InnerException.Message, GetCurrentUserAsync().Result.UserName);
                        return View(nameof(CreateRole), model);
                    }

                }
                else
                {
                    _toastNotification.AddToastMessage("Could not create role: " + model.RoleName, "", ToastEnums.ToastType.Error, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("Could not create role: " + model.RoleName, "Create Role", string.Empty, GetCurrentUserAsync().Result.UserName);
                    return View(nameof(CreateRole), model);
                }
            }
            else
            {
                return View(nameof(CreateRole), model);
            }
        }
        [HttpGet]
        [Route("/get/all/active/roles/json/")]
        public JsonResult ListRolesView()
        {

            try
            {
                var roles = _role.Query().ToList().Select(x => new RoleListViewModel
                {
                    Id = x.Id,
                    RoleName = x.Name
                });
                return Json(roles);
            }
            catch(Exception ex)
            {
                _toastNotification.AddToastMessage("An error ocurred", "", ToastEnums.ToastType.Error, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                _logger.InformationLog(ex.Message, "ListRolesView", ex.InnerException.Message, GetCurrentUserAsync().Result.UserName);
                return Json(false);
            }
            
        }

        public IActionResult ListRoles()
        {
            return View();
        }

        [HttpGet("/get/role/{Id}")]
        public IActionResult EditRoleView(int? Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var role = _roleManager.FindByIdAsync(Id.Value.ToString()).Result;
            var subModules = _manageUser.GetRoleModulesToSelectListItem(role.Id);
            var roleEdit = new EditRoleViewModel
            {
                Id = role.Id,
                RoleName = role.Name,
                ModuleList = subModules,

            };

            return View(roleEdit);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditRolePostView(EditRoleViewModel model,params string[] selectedModules)
        {
            if (ModelState.IsValid)
            {
                var res = _manageUser.EditRole(model, selectedModules).Result;
                if (res.Succeeded)
                {
                    _manageUser.UpdateClaimValueForRole();
                    _toastNotification.AddToastMessage("Role: " + model.RoleName + " edited succesfully", "", ToastEnums.ToastType.Success, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("Role " + model.RoleName + " edited succesfully", "Edit Role", string.Empty, GetCurrentUserAsync().Result.UserName);
                    return RedirectToAction("ListRoles");
                }
                else
                {
                    _toastNotification.AddToastMessage("Could not edit role: " + model.RoleName, "", ToastEnums.ToastType.Error, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    _logger.InformationLog("Role " + model.RoleName + " not edited succesfully", "Edit Role", string.Empty, GetCurrentUserAsync().Result.UserName);
                    return View(nameof(EditRoleView),model);
                }

               
            }
            else
            {
                _toastNotification.AddToastMessage("Could not edit role: " + model.RoleName, "", ToastEnums.ToastType.Error, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                _logger.InformationLog("Role " + model.RoleName + " not edited succesfully", "Edit Role", string.Empty, GetCurrentUserAsync().Result.UserName);
                return View(model);
            }
            
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteRoleView(int? Id)
        {
            if(Id == null)
            {
                return Json(false);
            }
            else
            {
                var role = await _roleManager.FindByIdAsync(Id.Value.ToString());
                var res = await _roleManager.DeleteAsync(role);
                if (res.Succeeded)
                {
                    _logger.InformationLog("Role " + role.Name + " deleted succesfully", "Delete Role", string.Empty, GetCurrentUserAsync().Result.UserName);
                    return Json(true);
                }
                else
                {
                    _logger.InformationLog("Role " + role.Name + " not deleted succesfully", "Delete Role", AddErrorList(res), GetCurrentUserAsync().Result.UserName);
                    return Json(false);
                }
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