"""Use the pinned trainer unchanged, adding only bounded block-stop conditions."""
import json
import os
import pathlib
import time


def main():
    from mlagents.trainers.trainer_controller import TrainerController
    from mlagents.trainers.learn import main as learn
    original = TrainerController._not_done_training
    started = time.monotonic()
    target = int(os.environ["DEMO_STOP_STEP"])
    soft_seconds = float(os.environ["DEMO_SOFT_SECONDS"])
    path = pathlib.Path(os.environ["DEMO_PROGRESS_FILE"])
    last = -10000

    def bounded(controller):
        nonlocal last
        steps = max((t.get_step for t in controller.trainers.values()), default=0)
        elapsed = time.monotonic() - started
        stop = steps >= target or elapsed >= soft_seconds
        if steps-last >= 1024 or stop:
            path.write_text(json.dumps(dict(trainerStep=steps, wallSeconds=elapsed,
                stopTarget=target, softLimitSeconds=soft_seconds,
                stopReason="planned block" if steps >= target else "soft wall limit" if stop else "running")))
            last = steps
        # Normal return lets the trainer join/drain and save/export through its
        # existing finally path; PPO/rewards/optimizer/update rules are untouched.
        return False if stop else original(controller)

    TrainerController._not_done_training = bounded
    learn()


if __name__ == "__main__":
    main()
