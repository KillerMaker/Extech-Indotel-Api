using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exatech_Indotel_API.Entities
{
    public class Role
    {
        public required int RoleId { get; set; }
        public required string RoleName { get; set; }
        public string? RoleDescription { get; set; }
    }
}
