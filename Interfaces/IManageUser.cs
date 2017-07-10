using System.Collections.Generic;
using System.Threading.Tasks;
using Itsomax.Module.Core.Extensions;
using Itsomax.Module.Core.Models;
using Itsomax.Module.UserManagement.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Itsomax.Module.UserManagement.Interfaces
{
    public interface IManageUser
    {
        Task<SucceededTask> CreateUser(CreateUserViewModel model,params string[] selectedRoles);
        Task<SucceededTask> DeleteUser(long Id);
        Task<SucceededTask> EditUser(EditUserViewModel model, params string[] rolesAdd);
        Task<SucceededTask> EditRole(EditRoleViewModel model, params string[] subModulesAdd);
        IEnumerable<SelectListItem> GetUserRolesToSelectListItem(int UserId);
        IEnumerable<SelectListItem> GetRoleModulesToSelectListItem(long RoleId);
        IList<string> GetSubmodulesByRoleId(long Id);
        bool CreateUserAddDefaultClaim(long Id);
        void UpdateClaimValueForRole();
        void CreateAdminfirstFirsRun();
    }
}