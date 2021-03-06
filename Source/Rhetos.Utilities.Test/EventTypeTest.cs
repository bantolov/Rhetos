﻿/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.Logging;

namespace Rhetos.Utilities.Test
{
    [TestClass]
    public class EventTypeTest
    {
        [TestMethod]
        public void Order()
        {
            var eventsOrderedByLevel = new[] { EventType.Trace, EventType.Info, EventType.Error };

            Assert.AreEqual(
                Enum.GetValues(typeof(EventType)).Length,
                eventsOrderedByLevel.Length,
                "This unit test should be updated with new enum values.");

            for (int i = 0; i < eventsOrderedByLevel.Length - 1; i++)
            {
                Assert.IsTrue(eventsOrderedByLevel[i] < eventsOrderedByLevel[i + 1]);
                Assert.IsTrue(eventsOrderedByLevel[i + 1] > eventsOrderedByLevel[i]);
            }
        }
    }
}
