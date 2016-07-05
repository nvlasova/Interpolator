﻿using Microsoft.Research.Oslo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Experiment {

    public interface IScnObj : INamedChild {
        List<string> AllParamsNames { get; }
        IEnumerable<IScnPrm> GetAllParams();
        Vector GetAllParamsValues(double t,Vector y);
        List<IScnObj> Children { get; }
        IScnPrm FindParam(string paramName);
        int AddParam(IScnPrm prm);
        void RemoveParam(IScnPrm prm);
        void AddChild(IScnObj child);
        void RemoveChild(IScnObj child);
        Vector Rebuild(double toTime);
        IEnumerable<IScnPrm> GetDiffPrms();
        void AddDiffPropToParam(IScnPrm prm, IScnPrm dPrmDt, bool removeOldDt, bool getNewName);
        void Resetparams();
        void SynchMe(double t);
        Action<double> SynchMeBefore { get; set; }
        Action<double> SynchMeAfter { get; set; }

        List<ILaw> Laws { get; }
        void AddLaw(ILaw newLaw);
        bool ApplyLaws();
    }

    public class ScnObjDummy : NamedChild, IScnObj {
        public IScnPrm[] DiffArr { get; private set; }
        public int DiffArrN { get; private set; } = -1;
        public List<IScnPrm> Prms { get; set; } = new List<IScnPrm>();
        public List<IScnObj> Children { get; set; } = new List<IScnObj>();
        public List<string> AllParamsNames { get; set; } = new List<string>();
        public List<ILaw> Laws { get; } = new List<ILaw>();
        public Action<double> SynchMeBefore { get; set; } = null;
        public Action<double> SynchMeAfter { get; set; }= null;

        public void SynchMeTo(double t,ref Vector y) {
            if (DiffArrN != y.Length)
                throw new InvalidOperationException("разные длины векторов in out");
     
            for (int i = 0; i < DiffArrN; i++) {
                DiffArr[i].SetVal(y[i]);
            }

            SynchMe(t);
        }

        public Vector f(double t, Vector y) {
            SynchMeTo(t,ref y);
            var result = Vector.Zeros(y.Length);
            for (int i = 0; i < DiffArrN; i++) {
                result[i] = DiffArr[i].MyDiff.GetVal(t);
            }
            return result;
        }

        public Vector Rebuild(double toTime = 0.0d) {
            if(!ApplyLaws())
                throw new Exception("Структура неправильная. Невозможно реализовать все Laws");

            AllParamsNames.Clear();
            foreach(var prm in GetAllParams()) {
                AllParamsNames.Add(prm.FullName);
            }

            var diffArrSeq = GetDiffPrms();
            foreach (var child in Children) {
                diffArrSeq = diffArrSeq.Concat(child.GetDiffPrms());
            }
            DiffArr = diffArrSeq.ToArray();
            DiffArrN = DiffArr.Length;
            return new Vector(diffArrSeq.Select(prm => prm.GetVal(toTime)).ToArray());
        }

        public bool ApplyLaws() {
            foreach(var law in Laws) {
                if(!law.ApplyMe())
                    return false;
            }

            foreach(var child in Children) {
                if(!child.ApplyLaws())
                    return false;
            }

            return true;
        }

        public IEnumerable<IScnPrm> GetDiffPrms() {
            return Prms.Where(a => a.MyDiff != null);
        }

        public void SynchMe(double t) {

            for(int i = 0; i < Prms.Count; i++) {
                if(Prms[i].IsNeedSynch)
                    Prms[i].SetVal(t);
            }

            SynchMeBefore?.Invoke(t);

            foreach(var child in Children) {
                child.SynchMe(t);
            }

            SynchMeAfter?.Invoke(t);
        }

        public int AddParam(IScnPrm prm) {
            if (!Prms.Contains(prm)) {
                if (prm.Owner != null && prm.Owner != this)
                    prm.Owner.RemoveParam(prm);

                Prms.Add(prm);
                prm.Owner = this;
            }
            return Prms.Count;
        }
        public void AddDiffPropToParam(IScnPrm prm, IScnPrm dPrmDt, bool removeOldDt = true, bool getNewName = true) {
            if (!Prms.Contains(prm))
                AddParam(prm);
            if (removeOldDt && prm.MyDiff != null && Prms.Contains(prm.MyDiff))
                Prms.Remove(prm.MyDiff);
            if (getNewName)
                dPrmDt.Name = prm.Name + "'";
            prm.MyDiff = dPrmDt;
            if(dPrmDt.Owner == null)
                AddParam(dPrmDt);
        }
        public void RemoveParam(IScnPrm prm) {
            if (Prms.Contains(prm))
                Prms.Remove(prm);
        }
        public void RemoveParam(String prmName) {
            var pX = FindParam(prmName);
            if(pX != null)
                RemoveParam(pX);
        }
        public IScnPrm FindParam(string paramName) {
            return Prms.Where(elem => elem.Name == paramName).FirstOrDefault();
        }

        public void AddChild(IScnObj newChild) {
            IScnObj hasWThisName = null;
            int ind = 0;
            do {
                hasWThisName = Children.FirstOrDefault(ch => ch.Name == newChild.Name);
                newChild.Name =
                    hasWThisName != null ?
                    Regex.Replace(hasWThisName.Name,@"\d+$","") + (++ind).ToString() :
                    newChild.Name;

            } while(hasWThisName != null);
            
            Children.Add(newChild);
            newChild.Owner = this;
        }
        public void RemoveChild(IScnObj child) {
            if (Children.Remove(child))
                child.Owner = null;
        }

        public Vector GetAllParamsValues(SolPoint sp) {
            return GetAllParamsValues(sp.T,sp.X);
        }
        public Vector GetAllParamsValues(double t,Vector y) {
            SynchMeTo(t,ref y);
            var res = Vector.Zeros(AllParamsNames.Count());
            int i = 0;
            foreach(var prm in GetAllParams()) {
                res[i++] = prm.GetVal(t);
            }
            return res;
        }
        public IEnumerable<IScnPrm> GetAllParams() {
            var res = Prms.Select(prm => prm);
            foreach(var child in Children) {
                res = res.Concat(child.GetAllParams());
            }
            return res;
        }

        public virtual void Resetparams() { }

        public void AddLaw(ILaw newLaw) {
            var alredyHas = Laws.FirstOrDefault(lw => lw.Name == newLaw.Name);

            if(alredyHas != null)
                throw new KeyNotFoundException("Такой закон уже тут есть!");

            Laws.Add(newLaw);
            newLaw.Owner = this;

        }
    }
}