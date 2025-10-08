using Soneta.Business;
using Soneta.Business.UI;
using Soneta.CRM;
using SonetaAddon;
using System;

[assembly: Worker(typeof(MyWorker1Worker), typeof(Kontrahent))]

namespace SonetaAddon
{
    public class MyWorker1Worker
    {


        [Context]
        public MyWorker1WorkerParams @params
        {
            get;
            set;
        }


        // TODO -> Należy podmienić podany opis akcji na bardziej czytelny dla uzytkownika
        [Action("MyWorker1Worker/ToDo", Mode = ActionMode.SingleSession | ActionMode.ConfirmSave | ActionMode.Progress)]
        public MessageBoxInformation ToDo()
        {
            @params.Session.Login.CheckAddinLicence();

            return new MessageBoxInformation("Potwierdzasz wykonanie operacji ?")
            {
                Text = "Opis operacji",
                YesHandler = () =>
                {
                    using (var t = @params.Session.Logout(true))
                    {
                        t.Commit();
                    }
                    return "Operacja została zakończona";
                },
                NoHandler = () => "Operacja przerwana"
            };

        }
    }


    public class MyWorker1WorkerParams : ContextBase
    {
        public MyWorker1WorkerParams(Context context) : base(context)
        {
        }

        // TODO -> Poniższy parametr dodany dla celów poglądowych. Należy usunąć.
        public string Parametr1 { get; set; }
    }

}
