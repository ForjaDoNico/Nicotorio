using Factory.Simulation;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Factory.Presentation
{
    /// <summary>
    /// The single entry point. When the Factory scene loads (it is empty on disk), this creates
    /// the World and every view/controller, and hands the World to each one through Init().
    /// Nothing else creates or looks up the simulation.
    /// </summary>
    public static class GameBootstrap
    {
        public const string SceneName = "Factory";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            if (SceneManager.GetActiveScene().name != SceneName) return;
            Build(new WorldConfig());
        }

        public static void Build(WorldConfig config)
        {
            var world = new World(config);
            var sprites = new SpriteLibrary();

            var root = new GameObject("Game");

            var runner = root.AddComponent<SimulationRunner>();
            runner.Init(world);

            var light = new GameObject("Sun (Global Light 2D)").AddComponent<Light2D>();
            light.transform.SetParent(root.transform, false);
            light.lightType = Light2D.LightType.Global;
            light.color = Palette.SunLight;
            light.intensity = 1f;

            var worldView = new GameObject("World").AddComponent<WorldView>();
            worldView.transform.SetParent(root.transform, false);
            worldView.Init(world, sprites);

            var buildingView = new GameObject("Buildings").AddComponent<BuildingView>();
            buildingView.transform.SetParent(root.transform, false);
            buildingView.Init(world, sprites);

            var itemView = new GameObject("Items").AddComponent<ItemView>();
            itemView.transform.SetParent(root.transform, false);
            itemView.Init(world, sprites);

            var player = new GameObject("Player").AddComponent<PlayerController>();
            player.transform.SetParent(root.transform, false);
            player.Init(world, sprites);

            var cameraFollow = new GameObject("Main Camera").AddComponent<CameraFollow>();
            cameraFollow.transform.SetParent(root.transform, false);
            cameraFollow.Init(player.transform, world.Width, world.Height);

            var input = new GameObject("Input").AddComponent<CommandInput>();
            input.transform.SetParent(root.transform, false);
            input.Init(world, runner, player, cameraFollow.Camera);

            var cursor = new GameObject("Cursor").AddComponent<CursorView>();
            cursor.transform.SetParent(root.transform, false);
            cursor.Init(world, input, player, sprites);

            var hud = new GameObject("HUD").AddComponent<HudView>();
            hud.transform.SetParent(root.transform, false);
            hud.Init(world, input);
        }
    }
}
