using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Diagnostics;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent( typeof( LightProbeGroup ) )]
public class LightProbeGenerator : MonoBehaviour 
{
   public enum EBakeType
   {
      Grid = 0,
   }

   [Tooltip("Toggle collisions checking when placing probes")]
   public bool checkForCollisions = false;

   [Tooltip( "Distance between probes." )]
   public float m_DistanceBetweenProbes = 5.0f;
   [Tooltip( "How do we bake the probes in this section?" )]
   public EBakeType m_BakeType;
   [Tooltip( "How big is this light probe generator." )]
   public Vector3 m_Size = Vector3.one * 10.0f;
   [Tooltip( "If true, this Light Probe Generator will draw the created light probes." )]
   public bool m_DrawLightProbeGizmos = false;

#if UNITY_EDITOR

   [ContextMenu( "Generate" )]
   public void Generate()
   {
      LightProbeGroup lightProbeGroup = GetComponent<LightProbeGroup>();

      try
      {
         switch ( m_BakeType )
         {
         case EBakeType.Grid:
            BakeGridProbes( lightProbeGroup );
            break;
         }
      }
      finally
      {
         UnityEngine.Debug.Log( String.Format("Generated {0} probes." , lightProbeGroup.probePositions.Length));
      }
   }

   private void BakeGridProbes( LightProbeGroup probe )
   {
      Vector3 min = Vector3.zero - ( m_Size * 0.5f );

      Vector3 probesPerAxis = Vector3.zero;
      probesPerAxis.x = Mathf.FloorToInt( m_Size.x / m_DistanceBetweenProbes );
      probesPerAxis.y = Mathf.FloorToInt( m_Size.y / m_DistanceBetweenProbes );
      probesPerAxis.z = Mathf.FloorToInt( m_Size.z / m_DistanceBetweenProbes );

      int amountOfProbesToCreate = Mathf.RoundToInt( probesPerAxis.x * probesPerAxis.y * probesPerAxis.z );
      {
         List<Vector3> probePositions = new List<Vector3>();

         Vector3 maxProbePositions = ( probesPerAxis - Vector3.one ) * m_DistanceBetweenProbes;
         Vector3 leftOver = m_Size - maxProbePositions;
         Vector3 startPosition = min + ( leftOver * 0.5f );

         for ( int x = 0; x < probesPerAxis.x; ++x )
         {
            for ( int y = 0; y < probesPerAxis.y; ++y )
            {
               for ( int z = 0; z < probesPerAxis.z; ++z )
               {
                  Vector3 probePosition = startPosition + ( new Vector3( x, y, z ) * m_DistanceBetweenProbes );

                  Vector3 worldProbePosition = transform.TransformPoint( probePosition );
                  if (!checkForCollisions || IsValidProbePosition( worldProbePosition ) )
                  {
                     probePositions.Add( probePosition );
                  }
               }
            }
         }

         probe.probePositions = probePositions.ToArray();
      }
   }

   private bool IsValidProbePosition( Vector3 worldProbePosition )
   {
      const float kRadius = 0.2f;
      const float kMaxHeightAboveGround = 100.0f;

      RaycastHit hitInfo;
      // Make sure 1) we're above ground. 2) We're not too high above ground.
      if ( !Physics.SphereCast( new Ray(worldProbePosition, Vector3.down), kRadius, out hitInfo, kMaxHeightAboveGround))
      {
         return false;
      }

      // Make sure we're not inside geometry.
      if ( Physics.CheckSphere( worldProbePosition, kRadius ) )
      {
         return false;
      }

      return true;
   }

   protected void OnDrawGizmosSelected()
   {
      Color yellow = Color.yellow;
      yellow.a = 0.2f;
      Gizmos.color = yellow;

      Matrix4x4 oldMatrix = Gizmos.matrix;
      Matrix4x4 rotationMatrix = Matrix4x4.TRS( transform.position, transform.rotation, transform.lossyScale );
      Gizmos.matrix = rotationMatrix;
      Gizmos.DrawCube( Vector3.zero, m_Size );

      Gizmos.matrix = oldMatrix;

      if ( m_DrawLightProbeGizmos )
      {
         yellow.a = 0.5f;
         Gizmos.color = yellow;
         foreach ( Vector3 position in GetComponent<LightProbeGroup>().probePositions )
         {
            Vector3 transformedPoint = transform.TransformPoint( position );
            Gizmos.DrawSphere( transformedPoint,0.2f);
         }
      }
   }

#endif
}
