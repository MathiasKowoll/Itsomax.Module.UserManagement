using System;
using System.ComponentModel.DataAnnotations;

namespace Itsomax.Module.UserManagement.ViewModels
{
    public class ChangePasswordUserView
    {
        public ChangePasswordUserView()
        {
        }
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
