using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Itsomax.Module.UserManagement.ViewModels
{
    public class CreateRoleViewModel
    {
        public string RoleName { get; set; }
        public IEnumerable<SelectListItem> ModuleList { get; set; }
    }
}