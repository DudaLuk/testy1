using Soneta.Business;
using Soneta.Business.App;
using Soneta.Config;
using SonetaAddon;


// Sposób w jaki należy zarejestrować extender, który później zostanie użyty w interfejsie.
[assembly: Worker(typeof(Konfiguracja))]



namespace SonetaAddon
{
    public class Konfiguracja
    {
        [Context]
        public Session Session { get; set; }

        [Context]
        public Login Login { get; set; }

        private const string Root = "HUBER";
        private const string Node = "HUBERnode";

        private void SetValue<T>(string name, T value, AttributeType type)
        {
            using (var t = Session.Logout(true))
            {
                var cfgManager = new CfgManager(Session);
                var node1 = cfgManager.Root.FindSubNode(Root, false) ?? cfgManager.Root.AddNode(Root, CfgNodeType.Node);
                var node2 = node1.FindSubNode(Node, false) ?? node1.AddNode(Node, CfgNodeType.Leaf);
                var attr = node2.FindAttribute(name, false);

                if (attr == null)
                    node2.AddAttribute(name, type, value);
                else
                    attr.Value = value;

                t.CommitUI();
            }
        }

        private T GetValue<T>(string name, T defaultValue)
        {
            var cfgManager = new CfgManager(Session);
            var node1 = cfgManager.Root.FindSubNode(Root, false);
            if (node1 == null) return defaultValue;

            var node2 = node1.FindSubNode(Node, false);
            if (node2 == null) return defaultValue;

            var attr = node2.FindAttribute(name, false);
            return attr == null || attr.Value == null ? defaultValue : (T)attr.Value;
        }



        public bool NIP
        {
            get => GetValue("NIP", false);
            set => SetValue("NIP", value, AttributeType._boolean);
        }
    }
}
