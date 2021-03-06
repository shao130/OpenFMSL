﻿using OpenFMSL.Core.Expressions;
using OpenFMSL.Core.Thermodynamics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFMSL.Core.ThermodynamicModels
{
    public class ActivityCoefficientNRTL : Expression
    {
        ThermodynamicSystem _system;
        int index = -1;
        int NC;

        Variable T;
        List<Variable> x;
        Expression[,] tau;
        Expression[,] G;
        Expression _gammaExp;
        Expression _dgammaDt;
        Expression[] _dgammaDx;

        private List<Expression> _parameters = new List<Expression>();

        public List<Expression> Parameters
        {
            get
            {
                return _parameters;
            }

            set
            {
                _parameters = value;
            }
        }

        public ActivityCoefficientNRTL(ThermodynamicSystem system, Variable T, List<Variable> x, int idx)
        {
            index = idx;
            _system = system;

            var parameterSet = _system.BinaryParameters.FirstOrDefault(ps => ps.Name == "NRTL");
            if (parameterSet == null)
                throw new ArgumentNullException("No NRTL parameters defined");

            double[,] a = parameterSet.Matrices["A"];
            double[,] b = parameterSet.Matrices["B"];
            double[,] c = parameterSet.Matrices["C"];
            double[,] d = parameterSet.Matrices["D"];
            double[,] e = parameterSet.Matrices["E"];
            double[,] f = parameterSet.Matrices["F"];

            Symbol = "NRTL_GAMMA";

            this.T = T;
            this.x = x;

            Parameters.Add(T);
            foreach (var comp in x)
                Parameters.Add(comp);

            NC = system.Components.Count;
            tau = new Expression[NC, NC];
            G = new Expression[NC, NC];

            int i = index;

            for (int ii = 0; ii < NC; ii++)
            {
                for (int j = 0; j < NC; j++)
                {

                    tau[ii, j] = a[ii, j];

                    if (b[ii, j] != 0.0)
                        tau[ii, j] += b[ii, j] / T;
                    if (e[ii, j] != 0.0)
                        tau[ii, j] += e[ii, j] * Sym.Ln(T);
                    if (f[ii, j] != 0.0)
                        tau[ii, j] += f[ii, j] * T;

                    Expression sij = c[ii, j];
                    if (d[ii, j] != 0.0)
                        sij += d[ii, j] * (T - 273.15);

                    if (c[ii, j] == 0.0 && d[i, j] == 0.0)
                        G[ii, j] = 1.0;
                    else
                        G[ii, j] = (Sym.Exp(-sij * tau[ii, j]));

                }
            }
            Expression lnGamma = 0.0;
            Expression S1 = 0.0;
            Expression[] S2 = new Expression[NC];
            Expression S3 = 0.0;
            for (int j = 0; j < NC; j++)
            {
                if (a[j, i] == 0.0 && b[j, i] == 0.0)
                    continue;
                if (c[j, i] == 0.0 && d[j, i] == 0.0)
                    continue;

                S1 += x[j] * tau[j, i] * G[j, i];
            }

            for (int ii = 0; ii < NC; ii++)
            {
                S2[ii] = 0.0;
                for (int k = 0; k < NC; k++)
                {                    
                    S2[ii] += x[k] * G[k, ii];
                }
            }
            for (int j = 0; j < NC; j++)
            {
                Expression S5 = 0.0;

                for (int m = 0; m < NC; m++)
                {
                    if (a[m, j] == 0.0 && b[m, j] == 0.0)
                        continue;
             

                    S5 += x[m] * tau[m, j] * G[m, j];
                }

                S3 += x[j] * G[i, j] / Sym.Par(S2[j]) * (tau[i, j] - S5 / Sym.Par(S2[j]));
            }

            lnGamma = S1 / S2[i] + S3;

            _gammaExp = Sym.Exp(lnGamma);
            _dgammaDx = new Expression[NC];

            DiffFunctional = (cache, v) => _gammaExp.Diff(cache, v);
            EvalFunctional = (cache) => _gammaExp.Eval(cache);
        }


        public override Expression SymbolicDiff(Variable var)
        {
            if (T == var)
            {
                if (_dgammaDt == null)
                    _dgammaDt = _gammaExp.SymbolicDiff(var);
                return _dgammaDt;
            }

            for (int i = 0; i < NC; i++)
            {
                if (x[i] == var)
                {
                    if (_dgammaDx[i] == null)
                        _dgammaDx[i] = _gammaExp.SymbolicDiff(var);
                    return _dgammaDx[i];
                }
            }
            return new IntegerLiteral(0);
        }

        public override HashSet<Variable> Incidence()
        {
            var inc = new HashSet<Variable>();

            foreach (var parameter in Parameters)
            {
                inc.UnionWith(parameter.Incidence());
            }
            return inc;
        }

        public override string ToString()
        {
            return "NRTL_GAMMA(T,x)";
        }
    }
}







