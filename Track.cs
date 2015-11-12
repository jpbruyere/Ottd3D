//
//  Track.cs
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
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Ottd3D
{
	public class Track
	{
		const int resolution = 20;

		vaoMesh trackMesh;

		public List<TrackSegment> Segments = new List<TrackSegment>();
		public TrackSegment CurrentSegment = null;

		public Track ()
		{
		}
			
		public void UpdateTrackMesh(Vector3 vEnd){
			if (trackMesh != null)
				trackMesh.Dispose ();
			trackMesh = null;

			List<Vector3> tp = new List<Vector3> ();

			for (int i = 0; i < Segments.Count; i++) {
				TrackSegment seg = Segments [i];
				float segLenght = (seg.EndPos - seg.StartPos).Length;
				float controlPointDist = segLenght * 0.49f;

				Vector3 secondCtrPoint;
				if (i < Segments.Count - 1)
					secondCtrPoint = seg.EndPos - Segments [i + 1].vStart * controlPointDist;
				else
					secondCtrPoint = seg.EndPos - vEnd * controlPointDist;

				Vector3[] p = new Vector3[]
				{
					seg.StartPos,
					seg.StartPos + seg.vStart * controlPointDist,
					secondCtrPoint,
					seg.EndPos
				};
				for (int j = 0; j < resolution-1; j++) {
					float t = (float)j / (float)(resolution-1);
					tp.Add(Path.CalculateBezierPoint (t, p [0], p [1], p [2], p [3]));
				}
			}

			if (tp.Count == 0)
				return;

			tp.Add (Segments [Segments.Count - 1].EndPos);

			trackMesh = new vaoMesh (tp.ToArray (), null, null);
		}

		public void Render()
		{
			if (trackMesh == null)
				return;
			trackMesh.Render (PrimitiveType.LineStrip);
		}
	}
}

