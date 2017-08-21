using System;
using System.ComponentModel.DataAnnotations;

namespace Itsomax.Module.UserManagement.ViewModels
{
    public class ChangePasswordUserViewModel
    {
        public long UserId { get; set; }
        [Display(Name = "Username")]
        public string UserName { get; set; }
		[Required]
		[DataType(DataType.Password)]
		public string NewPassword { get; set; }
		[Required]
		[DataType(DataType.Password)]
		[Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
		[Display(Name = "Confirm password")]
		public string ConfirmPassword { get; set; }
    }
}
