namespace DrillSnake
{
    /// <summary>
    /// Pure run-level progression that survives expedition and map resets.
    /// Scene presentation never owns or mutates the credit total directly.
    /// </summary>
    public sealed class DrillSnakeSession
    {
        public int BankedCredits { get; private set; }

        public int BankCargo(DrillSnakeSimulation simulation)
        {
            if (simulation == null ||
                !simulation.TryMarkCargoBanked(out var payoff))
            {
                return 0;
            }

            BankedCredits += payoff;
            return payoff;
        }

        public bool TrySpendCredits(int amount)
        {
            if (amount < 0 || BankedCredits < amount)
            {
                return false;
            }

            BankedCredits -= amount;
            return true;
        }

        public void ResolveFailedExpedition(DrillSnakeSimulation simulation)
        {
            simulation?.ResetExpedition();
        }
    }
}
