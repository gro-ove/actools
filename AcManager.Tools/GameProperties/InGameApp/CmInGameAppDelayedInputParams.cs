namespace AcManager.Tools.GameProperties.InGameApp {
    public class CmInGameAppDelayedInputParams : CmInGameAppParamsBase {
        public string CommandName;
        public double Progress;

        public CmInGameAppDelayedInputParams(string commandName) {
            CommandName = commandName;
        }
    }
}