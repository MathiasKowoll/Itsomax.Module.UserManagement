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
using Itsomax.Module.Core.Extensions.CommonHelpers;
using NToastNotify;
using System;

namespace Itsomax.Module.UserManagement.Controllers
{
    [Authorize(Policy = "ManageAuthentification")]
    public class RoleManagementController : Controller
    {
        private readonly RoleManager<Role> _roleManager;
        private readonly IRepository<ModuleRole> _modRoleRepository;
        private readonly IRepository<SubModule> _subModule;
        private readonly IRepository<Role> _role;
        private readonly IManageUser _manageUser;
        private IToastNotification _toastNotification;


        public RoleManagementController(RoleManager<Role> roleManager,IRepository<ModuleRole> modRoleRepository,
                                    IRepository<SubModule> subModule,IManageUser manageUser, IRepository<Role> role,
                                    IToastNotification toastNotification)
        {
            _roleManager = roleManager;
            _modRoleRepository = modRoleRepository;
            _subModule = subModule;
            _manageUser = manageUser;
            _role = role;
            _toastNotification = toastNotification;
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

            var res =_roleManager.CreateAsync(role).Result;
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
                    return RedirectToAction("ListRoles");
                }
                catch(Exception ex)
                {
                    _toastNotification.AddToastMessage("Could not create role: " + model.RoleName+" error: "+ex, "", ToastEnums.ToastType.Error, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    return View(model);
                }
                
            }
            else
            {
                _toastNotification.AddToastMessage("Could not create role: " + model.RoleName, "Error: "+res.Errors, ToastEnums.ToastType.Error, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                return View(model);
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
                _toastNotification.AddToastMessage("An error ocurred", "Error: " + ex, ToastEnums.ToastType.Error, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
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
                    return RedirectToAction("ListRoles");
                }
                else
                {
                    _toastNotification.AddToastMessage("Could not edit role: " + model.RoleName, "", ToastEnums.ToastType.Error, new ToastOption()
                    {
                        PositionClass = ToastPositions.TopCenter
                    });
                    return View(model);
                }

               
            }
            else
            {
                _toastNotification.AddToastMessage("Could not edit role: " + model.RoleName, "", ToastEnums.ToastType.Error, new ToastOption()
                {
                    PositionClass = ToastPositions.TopCenter
                });
                return View(model);
            }
            
        }

        [HttpDelete]
        public IActionResult DeleteRoleView(int? Id)
        {
            if(Id == null)
            {
                return Json(false);
            }
            else
            {
                var role =_roleManager.FindByIdAsync(Id.Value.ToString()).Result;
                var res = _roleManager.DeleteAsync(role).Result;
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
        
    }
}