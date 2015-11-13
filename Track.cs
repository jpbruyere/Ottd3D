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
using System.Linq;

namespace Ottd3D
{
	public class Track
	{
		const int resolution = 20;

		vaoMesh trackMesh;
		Tetra.IndexedVAO trackVao;

		public List<TrackSegment> TrackStarts = new List<TrackSegment>();
		public TrackSegment CurrentSegment = null;
		public Vector3 vEnd;

		public Track ()
		{
		}

		List<Vector3> tp = new List<Vector3> ();

		void buildSegment(TrackSegment tPrevious, TrackSegment tNext)
		{
			if (tPrevious != null) {
				float segLenght = (tPrevious.EndPos - tPrevious.StartPos).Length;
				float controlPointDist = segLenght * 0.49f;

				Vector3 secondCtrPoint;
				if (tNext == null) {
					if (tPrevious == CurrentSegment)
						secondCtrPoint = tPrevious.EndPos - vEnd * controlPointDist;
					else
						secondCtrPoint = tPrevious.EndPos - tPrevious.vStart * controlPointDist;
				}else
					secondCtrPoint = tPrevious.EndPos - tNext.vStart * controlPointDist;

				Vector3[] p = new Vector3[] {
					tPrevious.StartPos,
					tPrevious.StartPos + tPrevious.vStart * controlPointDist,
					secondCtrPoint,
					tPrevious.EndPos
				};
				for (int j = 0; j < resolution - 1; j++) {
					float t = (float)j / (float)(resolution - 1);
					tp.Add (Path.CalculateBezierPoint (t, p [0], p [1], p [2], p [3]));
				}

				if (tNext == null) {
					tp.Add (tPrevious.EndPos);
					return;
				}
			}
			if (tNext.NextSegment.Count == 0) {
				buildSegment(tNext, null);
				return;
			}

			foreach (TrackSegment ts in tNext.NextSegment ) {
				buildSegment (tNext, ts);
			}
		}

		public void UpdateTrackMesh(){
			if (trackMesh != null)
				trackMesh.Dispose ();
			trackMesh = null;

			foreach (TrackSegment tStart in TrackStarts)
				buildSegment (null, tStart);

			if (tp.Count == 0)
				return;

			trackMesh = new vaoMesh (tp.ToArray (), null, null);
			tp.Clear ();
		}

		public void Render()
		{
			if (trackMesh == null)
				return;
			trackMesh.Render (PrimitiveType.LineStrip);
		}
	}
}

