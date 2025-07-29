using System;
using System.Collections.Generic;
using System.Threading;

public class LoanService : ILoanService
{
    public LoanResult EvaluateLoanEligibility(string uid)
    {
        // Step 1: Find the user
        UserAccount user = null;
        foreach (var account in FakeLoanDatabase.Accounts)
        {
            if (account.UID == uid)
            {
                user = account;
                break;
            }
        }

        if (user == null)
        {
            throw new Exception("User not found");
        }

        // Step 2: Filter transactions from last 3 months
        DateTime cutoffDate = DateTime.Today.AddMonths(-3);
        List<Transaction> recentTransactions = new List<Transaction>();
        foreach (var tx in user.Transactions)
        {
            if (tx.Date >= cutoffDate)
            {
                recentTransactions.Add(tx);
            }
        }

        // Shared variables for parallel use
        double totalIncome = 0;
        double totalExpenses = 0;
        int salaryIncomeCount = 0;
        int overdraftCount = 0;

        object lockObj = new object();

        // Step 3: Parallel processing
        Parallel.ForEach(recentTransactions, tx =>
        {
            if (tx.Amount > 0)
            {
                Interlocked.Add(ref totalIncome, (long)(tx.Amount * 100)); // convert to long for atomic addition
                if (tx.Description != null && tx.Description.ToLower().Contains("salary"))
                {
                    Interlocked.Increment(ref salaryIncomeCount);
                }
            }
            else if (tx.Amount < 0)
            {
                Interlocked.Add(ref totalExpenses, (long)(Math.Abs(tx.Amount) * 100));
                if (Math.Abs(tx.Amount) > user.CurrentBalance)
                {
                    Interlocked.Increment(ref overdraftCount);
                }
            }
        });

        // Convert back to double from atomic-safe long representation
        double avgIncome = (totalIncome / 100.0) / 3.0;
        double avgExpense = (totalExpenses / 100.0) / 3.0;

        bool isIncomeStable = salaryIncomeCount >= 3;
        bool lowSpending = avgExpense < (0.5 * avgIncome);

        // Step 4: Loan and Interest calculation
        double multiplier = isIncomeStable ? 10 : 6;
        double maxLoan = (avgIncome - avgExpense) * multiplier;

        double rate = 10.0;
        if (user.CurrentBalance >= 5000) rate -= 1.5;
        if (lowSpending) rate -= 1.0;
        if (!isIncomeStable) rate += 1.5;
        if (overdraftCount > 0) rate += 2.0;

        return new LoanResult
        {
            MaxLoanAmount = Math.Round(maxLoan, 2),
            InterestRate = Math.Round(rate, 2)
        };
    }
}
