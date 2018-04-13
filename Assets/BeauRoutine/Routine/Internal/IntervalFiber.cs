﻿/*
 * Copyright (C) 2016-2018. Filament Games, LLC. All rights reserved.
 * Author:  Alex Beauchesne
 * Date:    3 Apr 2017
 * 
 * File:    IntervalFiber.cs
 * Purpose: Iterator enabling periodic updates of a nested fiber.
*/

using System;
using System.Collections;

namespace BeauRoutine.Internal
{
    /// <summary>
    /// Runs a fiber periodically.
    /// </summary>
    public sealed class IntervalFiber : IEnumerator, IDisposable, INestedFiberContainer
    {
        private Manager m_Manager;
        private IEnumerator m_Enumerator;
        private Fiber m_Fiber;
        private bool m_Iterating = false;
        private Fiber m_ParentFiber = null;

        private float m_Interval;
        private float m_Accumulation;

        public IntervalFiber(Manager inManager, IEnumerator inEnumerator, float inInterval)
        {
            m_Manager = inManager;
            m_Enumerator = inEnumerator;

            m_Interval = inInterval;
            m_Accumulation = 0;
        }

        public void RemoveFiber(Fiber inFiber)
        {
            if (!m_Iterating && m_Fiber == inFiber)
            {
                m_Fiber = null;
            }
        }

        public void SetParentFiber(Fiber inFiber)
        {
            m_ParentFiber = inFiber;
        }

        public void Dispose()
        {
            if (m_Enumerator != null)
            {
                ((IDisposable)m_Enumerator).Dispose();
                m_Enumerator = null;
            }

            if (m_Fiber != null)
            {
                m_Fiber.ClearNestedOwner();
                m_Fiber.Dispose();
                m_Fiber = null;
            }

            m_ParentFiber = null;
        }

        public object Current
        {
            get { return null; }
        }

        public bool MoveNext()
        {
            if (m_Enumerator != null)
            {
                m_Fiber = m_Manager.ChainFiber(m_Enumerator);
                m_Fiber.SetNestedOwner(this);
                m_Fiber.SetParentFiber(m_ParentFiber);

                m_Enumerator = null;
            }

            if (m_Fiber != null)
            {
                if (m_Iterating)
                    return true;
                
                m_Iterating = true;
                {
                    m_Accumulation += Routine.DeltaTime;
                    if (m_Accumulation > m_Interval)
                    {
                        // It's important that we reset timescale here.
                        // Otherwise timescale gets applied twice, resulting in inaccurate delta time values
                        float oldUnscaledTime = m_Manager.Frame.UnscaledDeltaTime;
                        float oldTimeScale = m_Manager.Frame.TimeScale;
                        m_Manager.Frame.UnscaledDeltaTime = m_Accumulation;
                        m_Manager.Frame.TimeScale = 1;
                        m_Manager.Frame.RefreshTimeScale();

                        m_Accumulation = 0;
                        
                        bool bNext = m_Fiber.Update();

                        m_Manager.Frame.UnscaledDeltaTime = oldUnscaledTime;
                        m_Manager.Frame.TimeScale = oldTimeScale;
                        m_Manager.Frame.RefreshTimeScale();

                        if (!bNext)
                        {
                            m_Fiber = null;
                            m_Iterating = false;
                            return false;
                        }
                    }
                }
                m_Iterating = false;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public override string ToString()
        {
            return "Routine::PerSecond()";
        }

        public RoutineStats[] GetStats()
        {
            if (m_Fiber == null)
                return new RoutineStats[0];

            RoutineStats[] stats = new RoutineStats[1];
            stats[0] = m_Fiber.GetStats();
            return stats;
        }
    }
}
