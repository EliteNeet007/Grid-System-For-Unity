using UnityEngine;

namespace MerelyGames.TerritoryControl
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TerritoryCellView : MonoBehaviour
    {
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _rendererColorId = Shader.PropertyToID("_RendererColor");

        private SpriteRenderer _spriteRenderer;
        private MaterialPropertyBlock _propertyBlock;

        public Vector2Int Position { get; private set; }

        /// <summary>
        /// Initializes this cell view with its board position, sprite, and optional material.
        /// </summary>
        public void Initialize(Vector2Int position, Sprite sprite, Material material)
        {
            Position = position;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _propertyBlock = new MaterialPropertyBlock();

            _spriteRenderer.sprite = sprite;
            if (material != null)
                _spriteRenderer.sharedMaterial = material;
        }

        /// <summary>
        /// Applies a color to the sprite renderer and common material color properties.
        /// </summary>
        public void SetColor(Color color)
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            _spriteRenderer.color = color;
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.SetColor(_baseColorId, color);
            _propertyBlock.SetColor(_colorId, color);
            _propertyBlock.SetColor(_rendererColorId, color);
            _spriteRenderer.SetPropertyBlock(_propertyBlock);
        }

        
    }
}
