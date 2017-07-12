using Itsomax.Module.UserManagement.Interfaces;
using Itsomax.Module.Core.Extensions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Itsomax.Module.Core.Models;
using System;
using Itsomax.Module.UserManagement.ViewModels;
using Itsomax.Data.Infrastructure.Data;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Xml.Linq;

namespace Itsomax.Module.UserManagement.Services
{
    public class ManageUser : IManageUser
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly SignInManager<User> _signIn;
        private readonly IRepository<User> _user;
        private readonly IRepository<Role> _role;
        private readonly IRepository<SubModule> _subModule;
        private readonly IRepository<ModuleRole> _moduleRole;

        public ManageUser(UserManager<User> userManager, RoleManager<Role> roleManager, SignInManager<User> signIn,
                         IRepository<Role> role, IRepository<User> user, IRepository<SubModule> subModule,
                         IRepository<ModuleRole> moduleRole)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _signIn = signIn;
            _user = user;
            _role = role;
            _subModule = subModule;
            _moduleRole = moduleRole;

        }

        public async Task<SucceededTask> CreateUser(CreateUserViewModel model, params string[] selectedRoles)
        {
            var user = new User()
            {
                Email = model.Email,
                UserName = model.UserName
            };
            var resUser = _userManager.CreateAsync(user, model.Password).Result;
            //var dummy =resUser.AsyncState.ToString();
            if (resUser.Succeeded)
            {
                //await _userManager.SetLockoutEnabledAsync(user, true);
                var resRole = _userManager.AddToRolesAsync(user, selectedRoles).Result;
                if (resRole.Succeeded)
                {
                    var resClaim = CreateUserAddDefaultClaim(user.Id);
                    if (resClaim)
                    {
                        UpdateClaimValueForRole();
                    }
                    return SucceededTask.Success;
                }
                else
                {
                    await DeleteUser(user.Id);
                    //TODO:add text based on database and languaje
                    return SucceededTask.Failed("ErrorUserCreate");
                }
            }
            else
            {
                return SucceededTask.Failed("ErrorUserCreate");
            }

        }
        public async Task<SucceededTask> DeleteUser(long Id)
        {
            var user = await _userManager.FindByIdAsync(Id.ToString());
            var resDelUSer = await _userManager.DeleteAsync(user);
            if (resDelUSer.Succeeded)
            {
                return SucceededTask.Success;
            }
            else
            {
                return SucceededTask.Failed("ErrorUserDelete");
            }
        }
        public async Task<SucceededTask> EditUser(EditUserViewModel model, params string[] rolesAdd)
        {
            var user = _userManager.FindByIdAsync(model.Id.ToString()).Result;

            user.Email = model.Email;
            user.UserName = model.UserName;
            user.IsDeleted = model.IsDeleted;


            var res = await _userManager.UpdateAsync(user);
            if (res.Succeeded)
            {
                var rolesRemove = _userManager.GetRolesAsync(user).Result;
                var resDel = await _userManager.RemoveFromRolesAsync(user, rolesRemove);
                if (resDel.Succeeded)
                {
                    var resAdd = await _userManager.AddToRolesAsync(user, rolesAdd);
                    if (resAdd.Succeeded)
                    {
                        if(model.IsLocked==true)
                        {
                            var resL = _userManager.SetLockoutEndDateAsync(user, Convert.ToDateTime("3000-01-01")).Result;
                            if (resL.Succeeded)
                            {
                                return SucceededTask.Success;
                            }
                            else
                                return SucceededTask.Failed("ErrorUserEdit");
                        }
                        else
                        {
                            var resL = _userManager.SetLockoutEndDateAsync(user, Convert.ToDateTime("1970-01-01")).Result;
                            if (resL.Succeeded)
                            {
                                return SucceededTask.Success;
                            }
                            else
                                return SucceededTask.Failed("ErrorUserEdit");
                        }
                        
                    }
                    else
                        return SucceededTask.Failed("ErrorUserEdit");
                }
                else
                    return SucceededTask.Failed("ErrorUserEditr");

            }
            else
            {
                return SucceededTask.Failed("ErrorUserEdit");
            }
        }

        public async Task<SucceededTask> EditRole(EditRoleViewModel model, params string [] subModulesAdd)
        {
            var role = _roleManager.FindByIdAsync(model.Id.ToString()).Result;
            role.Name = model.RoleName;

            var res = await _roleManager.UpdateAsync(role);
            if (res.Succeeded)
            {
                AddSubModulesToRole(role.Id,subModulesAdd);
                return SucceededTask.Success;
            }
            else
                return SucceededTask.Failed("FailedUpdateRole");

        }

        public void AddSubModulesToRole (long RoleId,params string[] subModules)
        {
            var modRole = _moduleRole.Query().Where(x => x.RoleId == RoleId);
            foreach (var item in modRole)
            {
                var modrole = _moduleRole.Query().FirstOrDefault(x => x.RoleId == item.RoleId && x.SubModuleId == item.SubModuleId);
                //ModuleRole modrole = new ModuleRole
                //{
                //    RoleId = item.RoleId,
                //    SubModuleId = item.SubModuleId
                //};
                _moduleRole.Remove(modrole);
            }
            _moduleRole.SaveChange();

            foreach (var item in subModules)
            {
                var mod = _subModule.Query().FirstOrDefault(x => x.Name.Contains(item));
                ModuleRole modrole = new ModuleRole
                {
                    RoleId = RoleId,
                    SubModuleId = mod.Id
                };
                _moduleRole.Add(modrole);
                
            }
            _moduleRole.SaveChange();
            UpdateClaimValueForRole();
        }



        public IEnumerable<SelectListItem> GetUserRolesToSelectListItem(int UserId)
        {
            var user = _userManager.FindByIdAsync(UserId.ToString()).Result;
            var userRoles = _userManager.GetRolesAsync(user).Result;
            var roles = _roleManager.Roles.ToList().Select(x => new SelectListItem()
            {
                Selected = userRoles.Contains(x.Name),
                Text = x.Name,
                Value = x.Name
            });

            return roles;
        }
        
        public IEnumerable<SelectListItem> GetRoleModulesToSelectListItem(long RoleId)
        {
            var role = _roleManager.FindByIdAsync(RoleId.ToString()).Result;
            var subModuleRole = GetSubmodulesByRoleId(role.Id);
            var subModule = _subModule.Query().ToList().Select(x=> new SelectListItem
            {
                Selected = subModuleRole.Contains(x.Name),
                Text = x.Name,
                Value = x.Name
            });

            return (subModule);
        }
        

        //public IList<string> GetSubmodules()
        //{
        //    return(_subModule.Query().Select(x => x.Name)).ToList();
        //}

        public IList<string> GetSubmodulesByRoleId(long Id)
        {
            var subModRole =
                from mr in _moduleRole.Query().ToList()
                join sb in _subModule.Query().ToList() on mr.SubModuleId equals sb.Id
                where mr.RoleId == Id
                select (sb.Name);

            return (subModRole.ToList());
        }

        public bool CreateUserAddDefaultClaim(long Id)
        {
            var user = _userManager.FindByIdAsync(Id.ToString()).Result;

            var claims = new List<Claim>();
            var claimsRemove = new List<Claim>();

            //claims.Add(new Claim("", ""));
            var claimsList = _subModule.Query().Select(x => new
            {
                x.Name

            }).ToList();
            var claimExistDB = _userManager.GetClaimsAsync(user).Result;
            foreach (var item in claimsList)
            {
                var claimExistDBType = claimExistDB.FirstOrDefault(x => x.Type == item.Name);
                if (claimExistDB.Count == 0)
                {
                    claims.Add(new Claim(item.Name, "NoAccess"));
                }

            }
            var res = _userManager.AddClaimsAsync(user, claims).Result;
            foreach (var item in claimExistDB)
            {
                var claimExistsDll = claimsList.FirstOrDefault(x => x.Name == item.Type);
                if (claimExistsDll == null)
                {
                    claims.Remove(new Claim(item.Type, item.Value));
                }

            }
            var resRem = _userManager.RemoveClaimsAsync(user, claimsRemove).Result;
            return true;
        }

        public void UpdateClaimValueForRole()
        {
            var users = _user.Query().ToList();
            foreach (var itemUser in users)
            {
                var user = _userManager.FindByIdAsync(itemUser.Id.ToString()).Result;
                var roles = _userManager.GetRolesAsync(user).Result;
                var rolesDB = _role.Query().Where(x => roles.Contains(x.Name)).ToList();
                var subModules = _subModule.Query().ToList();

                foreach (var subMod in subModules)
                {
                    var oldClaim = _userManager.GetClaimsAsync(user).Result.FirstOrDefault(x => x.Type == subMod.Name);
                    var newClaim = new Claim(subMod.Name, "NoAccess");
                    var res =_userManager.ReplaceClaimAsync(user, oldClaim, newClaim).Result;
                }
                
                foreach (var role in rolesDB)
                {
                    var subModulesUser = GetSubmodulesByRoleId(role.Id);
                    foreach (var item in subModulesUser)
                    {
                        var oldClaim = _userManager.GetClaimsAsync(user).Result.FirstOrDefault(x => x.Type == item);
                        var newClaim = new Claim(item, "HasAccess");
                        var res = _userManager.ReplaceClaimAsync(user, oldClaim, newClaim).Result;
                    }
                }
                //var rolesDB = _role.Query().Where(x => roles.Contains(x.Name)).ToList();
                /*
                foreach (var role in roles)
                {
                    var roleMod =
                        from r in rolesDB
                        join mr in _moduleRole.Query().ToList() on r.Id equals mr.RoleId
                        join sm in _subModule.Query().ToList() on mr.SubModuleId equals sm.Id
                        select (new { sm.Name });
                    var roleDistinct = roleMod.Select(x => x.Name).Distinct().ToList();
                    foreach (var item in roleDistinct)
                    {
                        var oldClaim = _userManager.GetClaimsAsync(user).Result.FirstOrDefault(x => x.Type == item);
                        var newClaim = new Claim(item, "HasAccess");
                        _userManager.ReplaceClaimAsync(user, oldClaim, newClaim);
                    }

                }
                */
            }

        }

        public void CreateAdminfirstFirsRun()
        {
            var roleExists = _roleManager.FindByNameAsync("Admin").Result;
            if (roleExists == null)
            {
                var role = new Role()
                {
                    Name = "Admin"
                };
                var res = _roleManager.CreateAsync(role).Result;
            }

            var userExists = _userManager.FindByNameAsync("admin").Result;
            if (userExists == null)
            {
                var user = new User()
                {
                    UserName = "admin",
                    Email = "admin@admin.cl",
                    LockoutEnabled= false
                };

                var res = _userManager.CreateAsync(user, "Admin123.,").Result;
                if (res.Succeeded)
                {
                    var res2 = _userManager.AddToRoleAsync(user, "Admin").Result;
                }
            }
        }
    }
}