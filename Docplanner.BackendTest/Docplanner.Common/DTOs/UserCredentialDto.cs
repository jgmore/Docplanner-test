using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Docplanner.Common.DTOs;

public class UserCredentialDto
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}

