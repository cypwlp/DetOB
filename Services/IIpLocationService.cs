using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OB.Services
{
    public interface IIpLocationService
    {
        Task<string> GetPublicIpAsync();
        string GetCountryByIp(string ip);
    }
}
