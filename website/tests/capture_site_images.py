from pathlib import Path

from playwright.sync_api import sync_playwright


ROOT = Path(__file__).resolve().parents[1]
STUDIO = Path(__file__).with_name("capture-studio.html")
OUTPUT = ROOT / "public" / "images"


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    targets = {
        "#product-hero": "product-hero.png",
        "#product-focus": "product-focus.png",
        "#product-modules": "product-modules.png",
    }

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, channel="chrome")
        page = browser.new_page(
            viewport={"width": 1640, "height": 940},
            device_scale_factor=1,
        )
        page.goto(STUDIO.as_uri(), wait_until="load")

        for selector, filename in targets.items():
            page.locator(selector).screenshot(
                path=str(OUTPUT / filename),
                animations="disabled",
            )

        browser.close()

    print("PASS: captured product-hero.png, product-focus.png, product-modules.png")


if __name__ == "__main__":
    main()
