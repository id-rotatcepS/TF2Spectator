using SimpleExpressionEvaluator;

using System;
using System.Text.RegularExpressions;

namespace TF2SpectatorWin
{
    public class MathChat
    {
        public MathChat()
        {
        }

        // only try messages that have at least two numbers and between them something mathish other than a decimal point.
        private readonly Regex mathRegex = new Regex("\\d.*[-+/*^\\p{IsMathematicalOperators}\\p{Sm}].*\\d");
        private readonly ExpressionEvaluator mathDoer = new ExpressionEvaluator();

        public string GetMathAnswer(string msg)
        {
            //consider stripping before/after text one might enter, like "what is () = ?"

            if (!mathRegex.IsMatch(msg))
                return null;

            string equationAnswer = GetEquationAnswer(msg);
            if (equationAnswer != null)
                return equationAnswer;

            return GetMathResponseOneSide(msg);
        }

        private string GetEquationAnswer(string msg)
        {
            string[] halves = msg.Split('=');
            if (halves.Length != 2)
                return null;

            string lhs = halves[0];
            string rhs = halves[1];

            string lhsAnswer = GetMathAnswerOneSide(lhs);
            string rhsAnswer = GetMathAnswerOneSide(rhs);
            if (lhsAnswer != null && rhsAnswer != null)
                return GetEqualityAnswer(lhs, rhs, lhsAnswer, rhsAnswer);

            if (lhsAnswer != null)
                return lhsAnswer + " = " + rhs;
            if (rhsAnswer != null)
                return lhs + " = " + rhsAnswer;
            return null;
        }

        private string GetMathAnswerOneSide(string msg)
        {
            try
            {
                return mathDoer.Evaluate(msg).ToString();
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
                return null;
            }
        }

        private string GetEqualityAnswer(string lhs, string rhs, string lhsAnswer, string rhsAnswer)
        {
            bool leftSimplified = !lhs.Trim().Equals(lhsAnswer.Trim());
            bool rightSimplified = !rhs.Trim().Equals(rhsAnswer.Trim());

            if (!lhsAnswer.Equals(rhsAnswer))
                return "No. " +
                    lhs + (leftSimplified ? (" = " + lhsAnswer) : "") +
                    " is not the same as " +
                    rhs + (rightSimplified ? (" = " + rhsAnswer) : "");

            if (leftSimplified && rightSimplified)
                return "Yes, " + lhsAnswer + " = " + lhs + " = " + rhs;
            else
                return "Yes, " + lhs + " = " + rhs;
        }

        private string GetMathResponseOneSide(string msg)
        {
            string mathAnswer = GetMathAnswerOneSide(msg);
            if (mathAnswer == null)
                return null;

            return (msg + " = " + mathAnswer);
        }
    }
}