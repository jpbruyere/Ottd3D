//
//  TrackSegment.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2015 jp
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using GGL;
using OpenTK;
using System.Collections.Generic;

namespace Ottd3D
{
	public class TrackSegment
	{
		Vector3[] Handles;
		public Vector3 StartPos, EndPos;
		public Vector3 vStart = Vector3.UnitX;

		public List<TrackSegment> PreviousSegment = new List<TrackSegment>();
		public List<TrackSegment> NextSegment = new List<TrackSegment>();

		public TrackSegment (Vector3 startPos) : this(startPos, Vector3.UnitX)
		{}
		public TrackSegment (Vector3 startPos, Vector3 _vStart){
			StartPos = startPos;
			vStart = _vStart;
			EndPos = StartPos + vStart;
		}
	}
}

