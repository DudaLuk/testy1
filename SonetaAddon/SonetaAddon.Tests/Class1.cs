using NUnit.Framework;
using Soneta.CRM;
using Soneta.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonetaAddon.Tests
{
    public class Class1: TestBase
    {

        [Test]
        public void ListowanieKontrahentów()
        {
           foreach (var k in Session.GetCRM().Kontrahenci.WgKodu)
            {
                Console.WriteLine(k.Nazwa);
            }



                
        }

    }
    
    
}
