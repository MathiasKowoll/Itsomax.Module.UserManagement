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

namespace Itsomax.Module.UserManagement.Controllers
{
    [Authorize(Policy = "ManageAuthentification")]
    public class RoleManagementController : Controller
    {
        private readonly RoleManager<Role> _roleManager;
        private readonly IRepository<ModuleRole> _modRoleRepository;
        private readonly IRepository<SubModule> _subModule;
        private readonly IManageUser _manageUser;


        public RoleManagementController(RoleManager<Role> roleManager,IRepository<ModuleRole> modRoleRepository,
                                    IRepository<SubModule> subModule,IManageUser manageUser)
        {
            _roleManager = roleManager;
            _modRoleRepository = modRoleRepository;
            _subModule = subModule;
            _manageUser = manageUser;
        }

        public IActionResult CreateRole()
        {
            _subModule.Query().Select(x => new SelectListItem
            {
                Value = x.Name,
                Text = StringHelperClass.CamelSplit(x.Name)
            });
            return View();
        }

        [HttpPost]
        public IActionResult CreateRolePostView(CreateRoleViewModel model, params string[] selectedModules)
        {
            Role role = new Role
            {
                Name = model.RoleName
            };

            var res =_roleManager.CreateAsync(role).Result;
            if (res.Succeeded)
            {
                foreach (var item  in selectedModules)
                {
                    var mod = _subModule.Query().FirstOrDefault(x => x.Name.Contains(item));
                    ModuleRole modrole = new ModuleRole
                    {
                        RoleId = role.Id,
                        SubModuleId = mod.Id
                    };
                    _modRoleRepository.Add(modrole);
                    _modRoleRepository.SaveChange();
                    _manageUser.UpdateClaimValueForRole();
                }
            }
            return RedirectToAction("RoleView");

        }

        public IActionResult ListRoles()
        {
            return View();
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
                    return Json(true);
                else
                    return Json(false);
                
            }
        }
        
    }
}