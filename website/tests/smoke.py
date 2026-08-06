from pathlib import Path
import json
import os
import re

from playwright.sync_api import Page, expect, sync_playwright


BASE_URL = os.environ.get("BASE_URL", "http://127.0.0.1:3000")
SCREENSHOTS = Path(__file__).parent / "screenshots-navigation-waterfall"
VIEWPORTS = [
    ("desktop", 1440, 900),
    ("tablet-wide", 1024, 900),
    ("tablet", 768, 900),
    ("mobile", 390, 844),
]


def settle_reveals_for_screenshot(page: Page) -> None:
    page.locator("[data-text-reveal]").evaluate_all(
        "(nodes) => nodes.forEach((node) => node.classList.add('isTextRevealed'))"
    )
    page.wait_for_timeout(80)


def assert_page_health(page: Page, path: str, expected_lang: str) -> None:
    console_errors: list[str] = []
    page.on(
        "console",
        lambda message: console_errors.append(message.text)
        if message.type == "error"
        else None,
    )

    response = page.goto(BASE_URL + path, wait_until="networkidle")
    assert response is not None and response.ok, f"{path} did not load successfully"
    expect(page.locator("html")).to_have_attribute("lang", expected_lang)
    expect(page.locator("h1")).to_have_count(1)
    expect(page.locator("main")).to_be_visible()

    overflow = page.evaluate(
        "() => document.documentElement.scrollWidth - window.innerWidth"
    )
    assert overflow <= 1, f"{path} overflows horizontally by {overflow}px"

    page.evaluate(
        """async () => {
          for (let y = 0; y < document.documentElement.scrollHeight; y += 600) {
            window.scrollTo(0, y);
            await new Promise((resolve) => setTimeout(resolve, 30));
          }
          await Promise.all([...document.images].map((image) => {
            if (image.complete && image.naturalWidth > 0) return Promise.resolve();
            return new Promise((resolve) => {
              image.addEventListener('load', resolve, { once: true });
              image.addEventListener('error', resolve, { once: true });
              setTimeout(resolve, 3000);
            });
          }));
          window.scrollTo(0, 0);
        }"""
    )
    page.wait_for_load_state("networkidle")
    broken_images = page.locator("img").evaluate_all(
        "(images) => images.filter((image) => !image.complete || image.naturalWidth === 0).map((image) => image.currentSrc || image.src)"
    )
    assert not broken_images, f"{path} contains broken images: {broken_images}"
    store_hrefs = page.locator("a").evaluate_all(
        "(links) => links.map((link) => link.href).filter((href) => href.includes('apps.microsoft.com'))"
    )
    assert store_hrefs, f"{path} is missing the Microsoft Store link"
    assert not console_errors, f"{path} console errors: {console_errors}"


def test_interactions(page: Page, locale: str) -> None:
    labels = (
        {
            "playing": "播放",
            "idle": "空闲",
            "near": "鼠标靠近",
        }
        if locale == "zh"
        else {
            "playing": "Playing",
            "idle": "Idle",
            "near": "Pointer nearby",
        }
    )

    demo_controls = page.locator(".demoControls")
    expect(demo_controls.get_by_role("button")).to_have_count(2)
    expect(
        demo_controls.get_by_role("button", name=labels["near"], exact=True)
    ).to_have_count(0)
    control_metrics = demo_controls.evaluate(
        """(controls) => {
          const buttons = [...controls.querySelectorAll("button")];
          const rects = buttons.map((button) => button.getBoundingClientRect());
          return {
            height: controls.getBoundingClientRect().height,
            buttonTops: rects.map((rect) => Math.round(rect.top)),
            buttonHeights: rects.map((rect) => rect.height)
          };
        }"""
    )
    assert control_metrics["height"] <= 64, "Demo controls should use a low single row"
    assert len(set(control_metrics["buttonTops"])) == 1, (
        "Playback controls should remain on one line"
    )
    assert all(height <= 36 for height in control_metrics["buttonHeights"])

    island = page.get_by_test_id("demo-island")
    island_path = island.locator(".demoIslandShape path")
    expect(island_path).to_have_attribute(
        "d",
        "M 0,0 L 560,0 C 532,0 522,5 516,15 C 512,22 512,34 512,40 "
        "C 512,49 504,55 491,55 L 69,55 C 56,55 48,49 48,40 "
        "C 48,34 48,22 44,15 C 38,5 28,0 0,0 Z",
    )

    page.get_by_role("button", name=labels["idle"], exact=True).click()
    expect(island).to_have_class(re.compile(r"\bisIdle\b"))
    page.wait_for_timeout(560)
    idle_metrics = island.evaluate(
        """(node) => {
          const desktop = node.closest(".demoDesktop").getBoundingClientRect();
          const rect = node.getBoundingClientRect();
          return {
            islandBottom: rect.bottom,
            desktopTop: desktop.top
          };
        }"""
    )
    assert idle_metrics["islandBottom"] <= idle_metrics["desktopTop"] + 1, (
        "Idle state should retract the whole island beyond the desktop edge"
    )

    page.get_by_role("button", name=labels["playing"], exact=True).click()
    expect(island).to_have_class(re.compile(r"\bisPlaying\b"))
    page.wait_for_timeout(560)
    island_box = island.bounding_box()
    assert island_box is not None
    page.mouse.move(
        island_box["x"] + island_box["width"] * 0.55,
        island_box["y"] + island_box["height"] * 0.5,
    )
    expect(island).to_have_class(re.compile(r"\bisNear\b"))
    page.wait_for_timeout(240)
    avoidance_metrics = island.evaluate(
        """(node) => ({
          mask: getComputedStyle(node.querySelector(".demoIslandSurface")).maskImage,
          webkitMask: getComputedStyle(
            node.querySelector(".demoIslandSurface")
          ).webkitMaskImage,
          glowCount: node.querySelectorAll(".demoAvoidanceGlow").length
        })"""
    )
    assert (
        avoidance_metrics["mask"] != "none"
        or avoidance_metrics["webkitMask"] != "none"
    ), "Pointer proximity should create a radial avoidance mask"
    assert avoidance_metrics["glowCount"] == 0, (
        "Pointer avoidance must reveal the desktop through the island, "
        "not paint a separate glow above the pointer"
    )
    near_radius = island.evaluate(
        "(node) => parseFloat(node.style.getPropertyValue('--avoid-radius-x'))"
    )
    near_radius_y = island.evaluate(
        "(node) => parseFloat(node.style.getPropertyValue('--avoid-radius-y'))"
    )
    near_opacity = island.evaluate(
        "(node) => parseFloat(node.style.getPropertyValue('--avoid-center-opacity'))"
    )
    near_transition_opacity = island.evaluate(
        "(node) => parseFloat(node.style.getPropertyValue('--avoid-transition-opacity'))"
    )
    assert abs(near_radius - 86 * (1.27 ** 0.5)) <= 0.1, (
        "Pointer avoidance should use the configured 86px aura size and "
        f"1.27:1 aspect ratio, got radiusX={near_radius}"
    )
    assert abs(near_radius / near_radius_y - 1.27) <= 0.01
    assert abs(near_opacity - 0.02) <= 0.001, (
        "Pointer avoidance should use 98% center transparency"
    )
    assert abs(near_transition_opacity - 0.03) <= 0.001, (
        "Pointer avoidance should use 97% transition transparency"
    )
    active_mask = avoidance_metrics["mask"] or avoidance_metrics["webkitMask"]
    assert "50.4%" in active_mask and "90%" in active_mask, (
        "The 56% transition and 0% edge transparency should match the "
        "desktop app's 0.9 radial-gradient mapping"
    )
    page.mouse.move(
        island_box["x"] + island_box["width"] * 0.55,
        island_box["y"] + island_box["height"] + 40,
    )
    expect(island).to_have_class(re.compile(r"\bisNear\b"))
    far_radius = island.evaluate(
        "(node) => parseFloat(node.style.getPropertyValue('--avoid-radius-x'))"
    )
    far_opacity = island.evaluate(
        "(node) => parseFloat(node.style.getPropertyValue('--avoid-center-opacity'))"
    )
    assert 0 < far_radius < near_radius, (
        "Pointer avoidance should shrink continuously as the pointer moves "
        f"away from the island, got near={near_radius}, far={far_radius}"
    )
    assert near_opacity < far_opacity < 1, (
        "Pointer avoidance should become less transparent with distance"
    )
    page.mouse.move(
        island_box["x"] + island_box["width"] * 0.55,
        island_box["y"] + island_box["height"] + 65,
    )
    expect(island).not_to_have_class(re.compile(r"\bisNear\b"))
    ended_radius = island.evaluate(
        "(node) => parseFloat(node.style.getPropertyValue('--avoid-radius-x'))"
    )
    assert ended_radius == 0, (
        "Pointer avoidance should reach zero before its mask is removed"
    )

    modules_section = page.locator("#modules")
    module_image = modules_section.locator(".moduleComposerImage")
    module_content = modules_section.locator(".moduleComposerContent")
    page.evaluate(
        "(section) => window.scrollTo(0, section.offsetTop)",
        modules_section.element_handle(),
    )
    page.wait_for_timeout(120)
    module_intro_metrics = modules_section.evaluate(
        """(section) => {
          const image = section.querySelector(".moduleComposerImage img");
          return {
            sectionHeight: section.offsetHeight,
            viewportHeight: window.innerHeight,
            imageFile: new URL(image.currentSrc).pathname.split("/").pop(),
            naturalWidth: image.naturalWidth,
            naturalHeight: image.naturalHeight,
            imageOpacity: Number(
              getComputedStyle(section.querySelector(".moduleComposerImage")).opacity
            ),
            contentOpacity: Number(
              getComputedStyle(section.querySelector(".moduleComposerContent")).opacity
            )
          };
        }"""
    )
    assert module_intro_metrics["sectionHeight"] > (
        module_intro_metrics["viewportHeight"] * 1.35
    ), "The modules anchor should provide in-section scroll distance"
    assert module_intro_metrics["imageFile"] == "module-layout-intro.png"
    assert module_intro_metrics["naturalWidth"] == 2560
    assert module_intro_metrics["naturalHeight"] == 1442
    assert module_intro_metrics["imageOpacity"] > 0.95
    assert module_intro_metrics["contentOpacity"] < 0.05
    module_frame_metrics = modules_section.locator(".moduleComposer").evaluate(
        """(composer) => {
          const outer = composer.getBoundingClientRect();
          const inner = composer.querySelector(".moduleComposerImage")
            .getBoundingClientRect();
          return {
            top: inner.top - outer.top,
            right: outer.right - inner.right,
            bottom: outer.bottom - inner.bottom,
            left: inner.left - outer.left,
            outerRadius: getComputedStyle(composer).borderRadius,
            innerRadius: getComputedStyle(
              composer.querySelector(".moduleComposerImage")
            ).borderRadius
          };
        }"""
    )
    assert all(
        abs(module_frame_metrics[edge] - 8) <= 0.5
        for edge in ["top", "right", "bottom", "left"]
    ), "The module image should have an even black frame on all four sides"
    assert module_frame_metrics["outerRadius"] == "32px"
    assert module_frame_metrics["innerRadius"] == "24px"

    page.evaluate(
        """(section) => {
          const range = section.offsetHeight - window.innerHeight;
          window.scrollTo(0, section.offsetTop + range * 0.72);
        }""",
        modules_section.element_handle(),
    )
    page.wait_for_timeout(180)
    module_reveal_metrics = modules_section.evaluate(
        """(section) => ({
          imageOpacity: Number(
            getComputedStyle(section.querySelector(".moduleComposerImage")).opacity
          ),
          contentOpacity: Number(
            getComputedStyle(section.querySelector(".moduleComposerContent")).opacity
          ),
          contentVisible:
            section.querySelector(".moduleComposer").dataset.contentVisible
        })"""
    )
    assert module_reveal_metrics["imageOpacity"] < 0.1
    assert module_reveal_metrics["contentOpacity"] > 0.9
    assert module_reveal_metrics["contentVisible"] == "true"

    faq = page.locator(".faqItem").first
    faq_button = faq.get_by_role("button")
    faq_button.click()
    expect(faq_button).to_have_attribute("aria-expanded", "true")
    page.wait_for_timeout(80)
    opening_height = faq.locator(".faqAnswer").evaluate(
        "(answer) => answer.getBoundingClientRect().height"
    )
    page.wait_for_timeout(520)
    open_height = faq.locator(".faqAnswer").evaluate(
        "(answer) => answer.getBoundingClientRect().height"
    )
    reduced_motion = page.evaluate(
        "matchMedia('(prefers-reduced-motion: reduce)').matches"
    )
    if reduced_motion:
        assert open_height > 0, "FAQ answers should still open without animation"
    else:
        assert 0 < opening_height < open_height, (
            "FAQ answers should expand progressively"
        )

    page.reload(wait_until="networkidle")
    page.keyboard.press("Tab")
    focus_data = page.evaluate(
        """() => {
          const node = document.activeElement;
          const style = window.getComputedStyle(node);
          return {
            tag: node?.tagName,
            outline: style.outlineStyle,
            width: node?.getBoundingClientRect().width || 0,
            height: node?.getBoundingClientRect().height || 0
          };
        }"""
    )
    assert focus_data["tag"] == "A", "The first keyboard target should be the skip link"
    assert focus_data["outline"] != "none", "Keyboard focus must be visible"


def test_smooth_section_snap(page: Page) -> None:
    page.goto(BASE_URL + "/", wait_until="networkidle")
    expect(page.locator("html")).to_have_attribute("data-snap-scroll", "enabled")

    sections = page.locator("[data-snap-section]")
    assert sections.count() >= 8, "The home page should expose major scroll anchors"
    expect(page.locator(".heroIsland")).to_have_count(0)
    expect(page.locator(".hero .eyebrow")).to_have_count(0)
    expect(page.locator("#experience > .experienceIntro > .eyebrow")).to_have_count(0)
    expect(page.locator("h1")).to_have_text("这一句，\n值得被看见。")
    expect(page.locator(".heroBadge")).to_have_count(0)
    assert "v2.0 Beta 3" not in page.locator("body").inner_text()

    hero_image = page.locator(".heroMediaImage")
    image_metrics = hero_image.evaluate(
        """(image) => ({
          naturalWidth: image.naturalWidth,
          naturalHeight: image.naturalHeight,
          width: image.getBoundingClientRect().width,
          height: image.getBoundingClientRect().height
        })"""
    )
    assert image_metrics["naturalWidth"] == 4000
    assert image_metrics["naturalHeight"] == 1334
    natural_ratio = image_metrics["naturalWidth"] / image_metrics["naturalHeight"]
    rendered_ratio = image_metrics["width"] / image_metrics["height"]
    assert abs(natural_ratio - rendered_ratio) < 0.02, (
        "The hero image must retain its original aspect ratio"
    )

    page.set_viewport_size({"width": 2048, "height": 1089})
    page.reload(wait_until="networkidle")
    page.locator("#players").evaluate(
        "(section) => window.scrollTo(0, section.offsetTop)"
    )
    page.wait_for_timeout(120)
    orbit_alignment = page.locator("#players").evaluate(
        """(section) => {
          const orbit = section.querySelector(".playerOrbit");
          return {
            sectionTop: section.getBoundingClientRect().top,
            sectionHeight: section.offsetHeight,
            orbitBottom: orbit.getBoundingClientRect().bottom,
            viewportHeight: window.innerHeight
          };
        }"""
    )
    assert abs(orbit_alignment["sectionTop"]) <= 2
    assert orbit_alignment["sectionHeight"] >= orbit_alignment["viewportHeight"]
    assert abs(orbit_alignment["orbitBottom"] - orbit_alignment["viewportHeight"]) <= 2, (
        "The player orbit should stay attached to the viewport bottom when zoomed out"
    )

    page.set_viewport_size({"width": 1440, "height": 900})
    page.reload(wait_until="networkidle")

    modules = page.locator("#modules")
    module_metrics = modules.evaluate(
        """(section) => ({
          top: section.offsetTop,
          height: section.offsetHeight,
          viewportHeight: window.innerHeight
        })"""
    )
    modules.evaluate("(section) => window.scrollTo(0, section.offsetTop)")
    page.wait_for_timeout(120)
    page.mouse.wheel(0, 18)
    page.wait_for_timeout(1800)
    expected_module_stage = (
        module_metrics["top"]
        + module_metrics["height"]
        - module_metrics["viewportHeight"]
    )
    assert abs(page.evaluate("window.scrollY") - expected_module_stage) <= 3, (
        "One wheel step should advance the staged module image completely"
    )
    assert modules.locator(".moduleComposerContent").evaluate(
        "(node) => Number(getComputedStyle(node).opacity)"
    ) > 0.95

    page.locator("#players").evaluate(
        "(section) => window.scrollTo(0, section.offsetTop)"
    )
    expect(page.locator("#players")).to_have_attribute("data-wheel-snap", "direct")
    page.locator("#players").evaluate(
        """(section) => {
          section.style.blockSize = "calc(100vh + 24px)";
          section.style.maxBlockSize = "none";
        }"""
    )
    page.locator("#players").evaluate(
        "(section) => window.scrollTo(0, section.offsetTop)"
    )
    sources = page.locator(".sourcesSection")
    sources_top = sources.evaluate("(section) => section.offsetTop")
    page.mouse.wheel(0, 18)
    page.wait_for_timeout(1800)
    assert abs(page.evaluate("window.scrollY") - sources_top) <= 3, (
        "One ordinary mouse-wheel step should advance to the next anchor "
        "even when viewport rounding makes the player section slightly taller"
    )
    sources_center = page.locator(".sourcesPanel").evaluate(
        "(panel) => (panel.getBoundingClientRect().top + panel.getBoundingClientRect().bottom) / 2"
    )
    available_center = page.evaluate(
        """() => (
          document.querySelector(".floatingNav").getBoundingClientRect().bottom
          + window.innerHeight
        ) / 2"""
    )
    assert abs(sources_center - available_center) <= 3

    page.evaluate("(top) => window.scrollTo(0, top + 48)", sources_top)
    page.wait_for_timeout(1800)
    assert abs(page.evaluate("window.scrollY") - sources_top) <= 3, (
        "A one-screen section must settle back onto its nearest anchor "
        "after native scrolling stops"
    )

    closing = page.locator(".closingSection")
    closing_top = closing.evaluate("(section) => section.offsetTop")
    closing.evaluate("(section) => window.scrollTo(0, section.offsetTop)")
    page.wait_for_timeout(120)
    assert abs(page.evaluate("window.scrollY") - closing_top) <= 3
    closing_center = page.locator(".closingPanel").evaluate(
        "(panel) => (panel.getBoundingClientRect().top + panel.getBoundingClientRect().bottom) / 2"
    )
    assert abs(closing_center - available_center) <= 3
    closing_buttons = closing.locator(".buttonRow .button")
    expect(closing_buttons).to_have_text(["GitHub", "Microsoft Store"])
    closing_button_sizes = closing_buttons.evaluate_all(
        """(buttons) => buttons.map((button) => ({
          width: button.getBoundingClientRect().width,
          height: button.getBoundingClientRect().height
        }))"""
    )
    assert closing_button_sizes[0] == closing_button_sizes[1]
    expect(closing_buttons.nth(0)).to_have_class(re.compile(r"\bbuttonPrimary\b"))
    expect(closing_buttons.nth(0)).to_have_attribute("href", re.compile(r"github\.com"))
    expect(closing_buttons.nth(1)).to_have_class(re.compile(r"\bbuttonSecondary\b"))
    expect(closing_buttons.nth(1)).to_have_attribute(
        "href", re.compile(r"apps\.microsoft\.com")
    )

    footer = page.locator(".siteFooter")
    footer_height = footer.evaluate("(node) => node.offsetHeight")
    assert footer_height >= page.evaluate("window.innerHeight")
    footer.evaluate("(node) => window.scrollTo(0, node.offsetTop)")
    page.wait_for_timeout(120)
    footer_panel_rect = footer.locator(":scope > .sectionContainer").evaluate(
        """(node) => {
          const rect = node.getBoundingClientRect();
          return { top: rect.top, bottom: rect.bottom };
        }"""
    )
    assert footer_panel_rect["top"] > 0, (
        "The final viewport should keep a white strip above the black footer panel"
    )
    assert footer_panel_rect["bottom"] >= page.evaluate("window.innerHeight - 1")

    page.goto(BASE_URL + "/updates", wait_until="networkidle")
    page.evaluate("window.scrollTo(0, document.documentElement.scrollHeight)")
    page.wait_for_timeout(100)
    updates_footer_bottom = page.locator(".updatesFooter").evaluate(
        "(footer) => footer.getBoundingClientRect().bottom"
    )
    assert updates_footer_bottom >= page.evaluate("window.innerHeight - 1")
    updates_footer_shadow = page.locator(".updatesFooter").evaluate(
        "(footer) => getComputedStyle(footer).boxShadow"
    )
    assert "64px" in updates_footer_shadow and "rgb(20, 20, 19)" in updates_footer_shadow

    page.goto(BASE_URL + "/", wait_until="networkidle")
    first_metrics = sections.nth(0).evaluate(
        "(section) => ({ top: section.offsetTop, height: section.offsetHeight })"
    )
    next_top = sections.nth(1).evaluate("(section) => section.offsetTop")
    viewport_height = page.evaluate("window.innerHeight")

    if first_metrics["height"] > viewport_height + 24:
        page.mouse.wheel(0, 240)
        page.wait_for_timeout(180)
        in_section_scroll = page.evaluate("window.scrollY")
        assert 0 < in_section_scroll < next_top, (
            "Long sections must keep ordinary scrolling before their bottom edge"
        )
        page.evaluate(
            "({ top, height }) => window.scrollTo(0, top + height - window.innerHeight)",
            first_metrics,
        )
        page.wait_for_timeout(80)

    page.mouse.wheel(0, 240)
    page.wait_for_timeout(420)
    early_scroll_top = page.evaluate("window.scrollY")
    start_top = max(0, first_metrics["top"] + first_metrics["height"] - viewport_height)
    progress = (early_scroll_top - start_top) / max(1, next_top - start_top)
    assert 0 < progress < 0.22, (
        "The original easing should begin more slowly than a linear transition"
    )

    page.wait_for_timeout(1600)
    scroll_top = page.evaluate("window.scrollY")
    assert abs(scroll_top - next_top) <= 3, (
        f"Smooth scrolling should settle on the next section; "
        f"expected {next_top}, got {scroll_top}"
    )

    page.get_by_role("link", name="主页", exact=True).click()
    page.wait_for_timeout(420)
    nav_progress_top = page.evaluate("window.scrollY")
    assert 0 < nav_progress_top < scroll_top, (
        "Navigation clicks should use the original nonlinear page animation"
    )
    page.wait_for_timeout(1800)
    assert abs(page.evaluate("window.scrollY")) <= 3

    faq = page.locator("#faq")
    faq.evaluate("(section) => window.scrollTo(0, section.offsetTop)")
    faq.locator(".faqQuestion button").evaluate_all(
        "(buttons) => buttons.forEach((button) => button.click())"
    )
    page.wait_for_timeout(700)
    expanded_faq = faq.evaluate(
        """(section) => ({
          top: section.offsetTop,
          height: section.offsetHeight,
          closingTop: document.querySelector(".closingSection").offsetTop,
          viewportHeight: window.innerHeight
        })"""
    )
    assert expanded_faq["height"] > expanded_faq["viewportHeight"], (
        "An expanded FAQ should grow beyond one viewport"
    )
    faq.evaluate("(section) => window.scrollTo(0, section.offsetTop)")
    page.mouse.wheel(0, 240)
    page.wait_for_timeout(180)
    faq_inner_scroll = page.evaluate("window.scrollY")
    assert expanded_faq["top"] < faq_inner_scroll < expanded_faq["closingTop"], (
        "Expanded FAQ content must scroll inside its own anchor before advancing"
    )

    for selector, next_selector in [
        ("#faq", ".closingSection"),
        (".closingSection", ".siteFooter"),
        (".siteFooter", None),
    ]:
        alignment = page.locator(selector).evaluate(
            """(section, nextSelector) => {
              const top = section.getBoundingClientRect().top + window.scrollY;
              window.scrollTo(0, top);
              const next = nextSelector ? document.querySelector(nextSelector) : null;
              return {
                top,
                height: section.offsetHeight,
                nextTop: next
                  ? next.getBoundingClientRect().top + window.scrollY
                  : null
              };
            }""",
            next_selector,
        )
        page.wait_for_timeout(60)
        settled_top = page.evaluate("window.scrollY")
        assert abs(settled_top - alignment["top"]) <= 3
        assert alignment["height"] >= viewport_height - 1, (
            f"{selector} should occupy at least one desktop viewport"
        )
        if alignment["nextTop"] is not None:
            assert alignment["nextTop"] - settled_top >= viewport_height - 1, (
                f"The next page should not peek into {selector}"
            )


def test_navigation_and_orbit(page: Page) -> None:
    page.goto(BASE_URL + "/", wait_until="networkidle")
    experience_images = page.locator("#experience .portraitImage")
    expect(experience_images).to_have_count(3)
    expect(page.locator("#experience .satelliteButton")).to_have_count(0)
    experience_image_metrics = experience_images.evaluate_all(
        """(images) => images.map((image) => ({
          file: new URL(image.currentSrc).pathname.split("/").pop(),
          naturalWidth: image.naturalWidth,
          naturalHeight: image.naturalHeight,
          objectFit: getComputedStyle(image).objectFit
        }))"""
    )
    assert experience_image_metrics == [
        {
            "file": "experience-playback.jpg",
            "naturalWidth": 1442,
            "naturalHeight": 1418,
            "objectFit": "cover",
        },
        {
            "file": "experience-idle.png",
            "naturalWidth": 2560,
            "naturalHeight": 1437,
            "objectFit": "cover",
        },
        {
            "file": "experience-pointer.png",
            "naturalWidth": 1638,
            "naturalHeight": 1638,
            "objectFit": "cover",
        },
    ]
    experience_frame_metrics = page.locator(
        "#experience .portraitWrap"
    ).evaluate_all(
        """(frames) => frames.map((frame) => {
          const rect = frame.getBoundingClientRect();
          const image = frame.querySelector(".portraitImage");
          return {
            width: rect.width,
            height: rect.height,
            borderRadius: getComputedStyle(frame).borderRadius,
            overflow: getComputedStyle(frame).overflow,
            imageShadow: getComputedStyle(image).boxShadow,
            backdrop: getComputedStyle(frame, "::before").content
          };
        })"""
    )
    assert all(
        abs(frame["width"] / frame["height"] - 1) <= 0.01
        and frame["borderRadius"] == "40px"
        and frame["overflow"] == "hidden"
        and frame["imageShadow"] == "none"
        and frame["backdrop"] == "none"
        for frame in experience_frame_metrics
    )
    if page.evaluate("window.innerWidth") >= 1024:
        experience_layout = page.locator("#experience").evaluate(
            """(section) => {
              const cards = [...section.querySelectorAll(".orbitCard")];
              const rect = (node) => {
                const box = node.getBoundingClientRect();
                return {
                  top: box.top,
                  right: box.right,
                  bottom: box.bottom,
                  left: box.left
                };
              };
              const canvas = section.querySelector(".orbitCanvas");
              const canvasRect = canvas.getBoundingClientRect();
              const cardsRect = section.querySelector(".orbitCards")
                .getBoundingClientRect();
              const introTitle = section.querySelector(".experienceIntro h2")
                .getBoundingClientRect();
              const introBody = section.querySelector(
                ".experienceIntro .sectionTitleGrid > p"
              ).getBoundingClientRect();
              return {
                canvas: {
                  top: canvasRect.top,
                  bottom: canvasRect.bottom,
                  height: canvasRect.height,
                  overflow: getComputedStyle(canvas).overflow
                },
                images: cards.map((card) => rect(card.querySelector(".portraitWrap"))),
                copies: cards.map((card) => rect(card.querySelector(".orbitCopy"))),
                imageOffsets: cards.map((card) => {
                  const image = card.querySelector(".portraitWrap")
                    .getBoundingClientRect();
                  return {
                    top: image.top - cardsRect.top,
                    left: image.left - cardsRect.left,
                    right: cardsRect.right - image.right
                  };
                }),
                cardsWidth: cardsRect.width,
                introBottoms: {
                  title:
                    section.querySelector(".experienceIntro h2").offsetTop
                    + section.querySelector(".experienceIntro h2").offsetHeight,
                  body:
                    section.querySelector(
                      ".experienceIntro .sectionTitleGrid > p"
                    ).offsetTop
                    + section.querySelector(
                      ".experienceIntro .sectionTitleGrid > p"
                    ).offsetHeight
                },
                cardBottoms: cards.map(
                  (card) => card.getBoundingClientRect().bottom
                )
              };
            }"""
        )
        assert experience_layout["copies"][0]["left"] >= (
            experience_layout["images"][0]["right"] + 24
        ), "Playback copy should sit to the right of the first image"
        assert experience_layout["copies"][1]["right"] <= (
            experience_layout["images"][1]["left"] - 24
        ), "Idle copy should sit to the left of the second image"
        assert experience_layout["copies"][2]["top"] >= (
            experience_layout["images"][2]["bottom"] + 24
        ), "Avoidance copy should remain below the third image"
        assert abs(experience_layout["imageOffsets"][0]["top"] - 120) <= 1
        assert abs(experience_layout["imageOffsets"][1]["top"] - 420) <= 1
        assert abs(experience_layout["imageOffsets"][2]["top"] - 150) <= 1
        assert abs(
            experience_layout["imageOffsets"][0]["left"]
            - experience_layout["cardsWidth"] * 0.02
        ) <= 1
        assert abs(
            experience_layout["imageOffsets"][1]["left"]
            - experience_layout["cardsWidth"] * 0.38
        ) <= 1
        assert abs(
            experience_layout["imageOffsets"][2]["right"]
            - experience_layout["cardsWidth"] * 0.01
        ) <= 1
        assert abs(
            experience_layout["introBottoms"]["title"]
            - experience_layout["introBottoms"]["body"]
        ) <= 2, (
            "The taskbar paragraph should align with the bottom of the main title; "
            f"titleBottom={experience_layout['introBottoms']['title']}, "
            f"bodyBottom={experience_layout['introBottoms']['body']}"
        )
        assert experience_layout["canvas"]["height"] <= 861
        assert experience_layout["canvas"]["overflow"] == "hidden"
        assert max(experience_layout["cardBottoms"]) <= (
            experience_layout["canvas"]["bottom"] + 1
        ), "The cropped experience canvas should still contain every card"

    first_experience_image = experience_images.first
    initial_experience_transform = first_experience_image.evaluate(
        "(image) => getComputedStyle(image).transform"
    )
    first_experience_image.hover()
    page.wait_for_timeout(240)
    hovered_experience_transform = first_experience_image.evaluate(
        "(image) => getComputedStyle(image).transform"
    )
    assert initial_experience_transform != hovered_experience_transform
    assert hovered_experience_transform.startswith("matrix(1.06")
    expect(page.locator(".factList article strong")).to_have_text(["4+", "6+", "0"])
    expect(page.locator(".factList article h3")).to_have_text(
        ["歌词来源", "主流播放器", "广告打扰"]
    )
    expect(page.locator(".sourcesNote")).to_have_text(
        "*受接口限制，网易云音乐暂不支持进度条同步与拖动进度条后的实时歌词同步。"
    )
    expect(page.locator(".heroSupport .buttonRow .button")).to_have_count(1)
    expect(page.locator(".heroSupport a[href*='apps.microsoft.com']")).to_have_count(1)
    expect(page.locator(".heroSupport a[href*='github.com']")).to_have_count(0)
    labels = page.locator(".desktopNavLinks a").all_text_contents()
    assert labels == [
        "主页",
        "新功能",
        "用户激励计划",
        "Microsoft Store",
    ]
    assert page.locator(".mobileMenuPanel > a").all_text_contents()[:4] == labels
    feature_link = page.locator(".desktopNavLinks .navFeatureLink")
    store_link = page.locator(".desktopNavLinks .navStoreLink")
    expect(feature_link).to_have_count(1)
    expect(feature_link.locator("svg")).to_have_count(0)
    expect(store_link.locator("svg")).to_have_count(1)
    store_arrow_stroke = store_link.locator("path").evaluate(
        "(path) => getComputedStyle(path).stroke"
    )
    assert store_arrow_stroke == "rgb(20, 20, 19)"

    page.goto(BASE_URL + "/en", wait_until="networkidle")
    expect(page.locator(".factList article strong")).to_have_text(["4+", "6+", "0"])
    expect(page.locator(".factList article h3")).to_have_text(
        ["lyric sources", "popular players", "ad interruptions"]
    )
    expect(page.locator(".sourcesNote")).to_have_text(
        "*Due to API limitations, NetEase Cloud Music currently does not support "
        "progress-bar synchronization or real-time lyric synchronization after you "
        "drag the progress bar."
    )
    assert page.locator(".desktopNavLinks a").all_text_contents() == [
        "Home",
        "What's new",
        "Community rewards",
        "Microsoft Store",
    ]
    assert page.locator(".mobileMenuPanel > a").all_text_contents()[:4] == [
        "Home",
        "What's new",
        "Community rewards",
        "Microsoft Store",
    ]
    expect(page.locator(".desktopNavLinks .navFeatureLink svg")).to_have_count(0)
    expect(page.locator(".desktopNavLinks .navStoreLink svg")).to_have_count(1)

    page.goto(BASE_URL + "/", wait_until="networkidle")
    player_orbit = page.locator(".playerOrbit")
    player_orbit.hover()

    orbiting_player = page.locator(".playerPill1")
    initial_position = orbiting_player.evaluate(
        "(pill) => ({ x: pill.getBoundingClientRect().x, y: pill.getBoundingClientRect().y })"
    )
    page.wait_for_timeout(700)
    later_position = orbiting_player.evaluate(
        "(pill) => ({ x: pill.getBoundingClientRect().x, y: pill.getBoundingClientRect().y })"
    )
    assert initial_position != later_position, (
        "White player pills should keep moving along the arc while it is hovered"
    )

    orbit_metrics = player_orbit.evaluate(
        "(orbit) => ({ height: orbit.offsetHeight, overflow: getComputedStyle(orbit).overflow })"
    )
    assert orbit_metrics["height"] == 340
    assert orbit_metrics["overflow"] == "hidden"
    visible_fractions = page.locator(".playerArcLines ellipse").evaluate_all(
        """(ellipses) => ellipses.map((ellipse) => {
          const cy = Number(ellipse.getAttribute('cy'));
          const ry = Number(ellipse.getAttribute('ry'));
          return (340 - (cy - ry)) / (2 * ry);
        })"""
    )
    assert all(abs(fraction - 2 / 3) <= 0.001 for fraction in visible_fractions), (
        f"Each ellipse should retain its upper two thirds, got {visible_fractions}"
    )
    visible_pills = page.locator(".playerPill:not(.playerPill7)").evaluate_all(
        """(pills) => pills.filter((pill) => {
          const rect = pill.getBoundingClientRect();
          const orbit = pill.parentElement.getBoundingClientRect();
          return rect.right > orbit.left && rect.left < orbit.right
            && rect.bottom > orbit.top && rect.top < orbit.bottom;
        }).length"""
    )
    assert 5 <= visible_pills <= 6, (
        f"The cropped arc should show five or six queued player pills, got {visible_pills}"
    )
    middle_pill_index = page.locator(".playerPill:not(.playerPill7)").evaluate_all(
        """(pills) => {
          const orbit = pills[0].parentElement.getBoundingClientRect();
          return pills.findIndex((pill) => {
            const rect = pill.getBoundingClientRect();
            const center = rect.left + rect.width / 2;
            return center > orbit.left + orbit.width * 0.25
              && center < orbit.left + orbit.width * 0.65;
          });
        }"""
    )
    assert middle_pill_index >= 0
    middle_pill = page.locator(".playerPill:not(.playerPill7)").nth(middle_pill_index)
    middle_x = middle_pill.evaluate(
        "(pill) => pill.getBoundingClientRect().left + pill.getBoundingClientRect().width / 2"
    )
    page.wait_for_timeout(500)
    assert middle_pill.evaluate(
        "(pill) => pill.getBoundingClientRect().left + pill.getBoundingClientRect().width / 2"
    ) > middle_x, "Player pills should travel from the left queue to the right edge"
    section_height = page.locator(".compatibilitySection").evaluate(
        "(section) => section.offsetHeight"
    )
    assert abs(section_height - page.evaluate("innerHeight")) <= 1

    center_error = page.locator(".playerPill7").evaluate(
        """(pill) => {
          const orbit = pill.parentElement.getBoundingClientRect();
          const rect = pill.getBoundingClientRect();
          return {
            x: Math.abs((rect.left + rect.width / 2) - (orbit.left + orbit.width / 2)),
            y: Math.abs((rect.top + rect.height / 2) - (orbit.top + orbit.height / 2))
          };
        }"""
    )
    assert center_error["x"] <= 1 and center_error["y"] <= 1, (
        f"The SMTC pill should be centered within 1px, got {center_error}"
    )


def test_selective_text_reveal(page: Page, reduced_motion: bool = False) -> None:
    page.goto(BASE_URL + "/?motion-test=1", wait_until="networkidle")
    page.evaluate("window.scrollTo(0, 0)")
    page.wait_for_timeout(850)
    reveal_targets = page.locator("[data-text-reveal]")
    all_copy = page.locator("h1, h2, h3, p")
    assert 8 <= reveal_targets.count() < all_copy.count(), (
        "Text reveals should be reserved for a small set of chapter headings"
    )
    expect(page.locator(".heroSupport > p[data-text-reveal]")).to_have_count(0)
    expect(page.locator(".faqQuestion[data-text-reveal]")).to_have_count(0)

    modules_title = page.locator("#modules h2[data-text-reveal]")
    if reduced_motion:
        assert modules_title.evaluate("(node) => getComputedStyle(node).opacity") == "1"
        duration = modules_title.evaluate(
            "(node) => getComputedStyle(node).transitionDuration"
        )
        assert duration in {"0s", "0.00001s", "1e-05s"}
        return

    expect(page.locator("html")).to_have_class(re.compile(r"\btextRevealReady\b"))
    assert not modules_title.evaluate(
        "(node) => node.classList.contains('isTextRevealed')"
    ), "Off-screen headings should wait until their section enters the viewport"
    hidden_state = modules_title.evaluate(
        "(node) => ({ opacity: getComputedStyle(node).opacity, transform: getComputedStyle(node).transform })"
    )
    assert float(hidden_state["opacity"]) <= 0.02
    assert hidden_state["transform"] != "none"

    modules_title.scroll_into_view_if_needed()
    page.wait_for_timeout(80)
    expect(modules_title).to_have_class(re.compile(r"\bisTextRevealed\b"))
    page.wait_for_timeout(1050)
    assert modules_title.evaluate("(node) => getComputedStyle(node).opacity") == "1"


def test_incentive_page(page: Page, path: str, lang: str, mobile: bool = False) -> None:
    errors: list[str] = []
    page.on("console", lambda message: errors.append(message.text) if message.type == "error" else None)
    page.route(
        "**/api/incentives/public",
        lambda route: route.fulfill(
            status=200,
            content_type="application/json",
            body=(
                '{"configured":true,"suggestions":['
                '{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01b001","nickname":"岛民小林","title":"自动切换歌词布局",'
                '"body":"根据窗口状态切换单行和双行歌词。","created_at":"2026-07-14T00:00:00Z",'
                '"like_count":12,"liked":false,'
                '"attachment":{"name":"演示.mp4","type":"video/mp4","url":"https://example.com/demo.mp4"}},'
                '{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01b002","nickname":"青柠","title":"专注模式",'
                '"body":"全屏工作时只显示当前一句歌词。","created_at":"2026-07-13T00:00:00Z","like_count":8,"liked":false},'
                '{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01b003","nickname":"Sea","title":"播放器快速切换",'
                '"body":"多个会话同时存在时可以快速锁定播放器。","created_at":"2026-07-12T00:00:00Z","like_count":5,"liked":true},'
                '{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01b004","nickname":"九月","title":"布局预设",'
                '"body":"为不同桌面保存独立的歌词岛布局。","created_at":"2026-07-11T00:00:00Z","like_count":3,"liked":false}'
                '],"previews":['
                '{"id":"preview-1","version":"v2.1 Preview","title_zh":"更安静的桌面交互",'
                '"title_en":"Quieter desktop interactions","body_zh":"继续打磨避让。\\n- 收起体验更加顺滑。",'
                '"body_en":"More polish for avoidance.\\n- Smoother retraction.","highlights_zh":[],'
                '"highlights_en":[],"target_date":"2026-09-01",'
                '"status":"published","created_at":"2026-07-14T00:00:00Z",'
                '"updated_at":"2026-07-14T00:00:00Z","published_at":"2026-07-14T00:00:00Z"}]}'
            ),
        ),
    )
    page.route(
        "**/api/incentives/likes",
        lambda route: route.fulfill(
            status=200,
            content_type="application/json",
            body='{"liked":true,"like_count":13}',
        ),
    )
    response = page.goto(BASE_URL + path, wait_until="networkidle")
    assert response is not None and response.ok
    expect(page.locator("html")).to_have_attribute("lang", lang)
    expect(page.locator("h1")).to_have_count(1)
    assert page.evaluate("document.documentElement.scrollWidth - innerWidth") <= 1
    assert page.locator(".acceptedCard").count() >= 16
    assert 0 < page.locator(".acceptedAttachment").count() < page.locator(".acceptedCard").count()
    preview_card = page.locator(".previewCard")
    expect(preview_card.locator(".previewCardMeta")).to_contain_text("v2.1 Preview")
    expect(preview_card.locator(".previewItemNumber")).to_have_text(["01", "02"])
    expect(preview_card.locator(".previewItems p")).to_have_text(
        ["More polish for avoidance.", "Smoother retraction."] if lang == "en" else ["继续打磨避让。", "收起体验更加顺滑。"]
    )
    expect(page.locator(".acceptedTime").first).to_be_visible()
    expect(page.locator(".acceptedWaterfallColumn")).to_have_count(4)
    visible_columns = page.locator(".acceptedWaterfallColumn").evaluate_all(
        "(columns) => columns.filter((column) => getComputedStyle(column).display !== 'none').length"
    )
    assert visible_columns == (2 if mobile else 4)
    mask = page.locator(".acceptedWaterfallViewport").evaluate(
        "(node) => getComputedStyle(node).maskImage || getComputedStyle(node).webkitMaskImage"
    )
    reduced_motion = page.evaluate(
        "matchMedia('(prefers-reduced-motion: reduce)').matches"
    )
    if reduced_motion:
        assert mask == "none", "Static reduced-motion columns should remain plainly scrollable"
    else:
        assert "linear-gradient" in mask, "Waterfall edges should use a vertical fade mask"
    expect(page.locator(".previewCard")).to_have_count(1)
    stage_angle = page.locator(".acceptedWaterfall").evaluate(
        """(stage) => {
          const matrix = new DOMMatrix(getComputedStyle(stage).transform);
          return Math.atan2(matrix.b, matrix.a) * 180 / Math.PI;
        }"""
    )
    if reduced_motion:
        assert abs(stage_angle) <= 0.1
    else:
        assert abs(stage_angle - 45) <= 0.1, f"Waterfall stage should rotate 45 degrees, got {stage_angle}"

    card_transform = page.locator(".acceptedCard").first.evaluate(
        "(card) => getComputedStyle(card).transform"
    )
    assert card_transform == "none", "Cards should inherit the stage angle without individual rotation"

    card_surfaces = page.locator(".acceptedWaterfallColumn").first.locator(".acceptedCard").evaluate_all(
        """(cards) => cards.slice(0, 3).map((card) => {
          const style = getComputedStyle(card);
          return { background: style.backgroundColor, border: style.borderTopColor };
        })"""
    )
    assert len({surface["background"] for surface in card_surfaces}) == 3
    assert all(surface["background"] == surface["border"] for surface in card_surfaces)

    seamless_cycle = page.locator(".acceptedWaterfallColumn").first.evaluate(
        """(column) => {
          const frames = [...column.querySelectorAll('.acceptedCardFrame')];
          const half = frames.length / 2;
          return frames.slice(0, half).map((frame, index) => {
            const originalCard = frame.querySelector('.acceptedCard');
            const duplicateFrame = frames[index + half];
            const duplicateCard = duplicateFrame.querySelector('.acceptedCard');
            return {
              originalCycleIndex: frame.dataset.cycleIndex,
              duplicateCycleIndex: duplicateFrame.dataset.cycleIndex,
              originalSurface: getComputedStyle(originalCard).backgroundColor,
              duplicateSurface: getComputedStyle(duplicateCard).backgroundColor,
              originalTitle: originalCard.querySelector('h3').textContent,
              duplicateTitle: duplicateCard.querySelector('h3').textContent
            };
          });
        }"""
    )
    assert all(
        pair["originalCycleIndex"] == pair["duplicateCycleIndex"]
        and pair["originalSurface"] == pair["duplicateSurface"]
        and pair["originalTitle"] == pair["duplicateTitle"]
        for pair in seamless_cycle
    ), "Both waterfall halves must be pixel-equivalent at the animation seam"

    spacing = page.locator(".acceptedWaterfallColumn").first.evaluate(
        """(column) => {
          const frames = [...column.querySelectorAll('.acceptedCardFrame')].slice(0, 3);
          const card = frames[0].querySelector('.acceptedCard');
          return {
            first: frames[1].offsetTop - frames[0].offsetTop,
            second: frames[2].offsetTop - frames[1].offsetTop,
            cardHeight: card.offsetHeight
          };
        }"""
    )
    assert abs(spacing["first"] - spacing["second"]) <= 1
    assert spacing["first"] - spacing["cardHeight"] >= 16

    adjacent_gap = page.locator(".acceptedWaterfall").evaluate(
        """(stage) => {
          const columns = stage.querySelectorAll('.acceptedWaterfallColumn');
          const first = columns[0].querySelector('.acceptedCard');
          const second = columns[1].querySelector('.acceptedCard');
          const firstLeft = columns[0].offsetLeft + first.offsetLeft;
          const secondLeft = columns[1].offsetLeft + second.offsetLeft;
          return secondLeft - (firstLeft + first.offsetWidth);
        }"""
    )
    assert adjacent_gap >= 0, f"Adjacent card columns should not overlap, got {adjacent_gap}px"

    tracks = page.locator(".acceptedWaterfallTrack")
    if reduced_motion:
        animation_name = tracks.first.evaluate("(node) => getComputedStyle(node).animationName")
        overflow_x = page.locator(".acceptedWaterfallViewport").evaluate(
            "(node) => getComputedStyle(node).overflowX"
        )
        assert animation_name == "none"
        assert overflow_x in {"auto", "scroll"}
    else:
        initial_positions = [
            tracks.nth(index).evaluate(
                "(node) => ({ x: node.getBoundingClientRect().x, y: node.getBoundingClientRect().y })"
            )
            for index in range(2)
        ]
        page.wait_for_timeout(700)
        later_positions = [
            tracks.nth(index).evaluate(
                "(node) => ({ x: node.getBoundingClientRect().x, y: node.getBoundingClientRect().y })"
            )
            for index in range(2)
        ]
        assert later_positions[0]["x"] > initial_positions[0]["x"]
        assert later_positions[0]["y"] < initial_positions[0]["y"]
        assert later_positions[1]["x"] < initial_positions[1]["x"]
        assert later_positions[1]["y"] > initial_positions[1]["y"]
    page.locator(".acceptedWaterfallViewport").hover()
    page.wait_for_timeout(120)
    like_buttons = page.locator(".acceptedLikeButton")
    clickable_indexes = like_buttons.evaluate_all(
        """(buttons) => buttons.flatMap((button, index) => {
          const rect = button.getBoundingClientRect();
          const x = rect.left + rect.width / 2;
          const y = rect.top + rect.height / 2;
          const hit = document.elementFromPoint(x, y);
          const count = button.textContent?.trim();
          return count?.endsWith('12') && x > 0 && x < innerWidth && y > 0 && y < innerHeight
            && hit && (hit === button || button.contains(hit))
            ? [index]
            : [];
        })"""
    )
    assert clickable_indexes, "At least one visible suggestion with 12 likes must be directly clickable"
    like_button = like_buttons.nth(clickable_indexes[0])
    expect(like_button).to_contain_text("12")
    like_button.click()
    expect(like_button).to_have_attribute("aria-pressed", "true")
    expect(like_button).to_contain_text("13")

    if lang == "zh-CN":
        page.route(
            "**/api/incentives/submissions",
            lambda route: route.fulfill(
                status=201,
                content_type="application/json",
                body='{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01c001","status":"pending"}',
            ),
        )
        page.get_by_label("昵称").fill("测试岛民")
        page.get_by_label("邮箱").fill("islander@example.com")
        page.get_by_label("一句话标题").fill("希望加入更灵活的歌词行数")
        page.get_by_label("详细说明").fill("在窗口较窄时希望可以自动切换为单行歌词显示。")
        page.get_by_role("button", name="提交新功能提议").click()
        expect(page.get_by_role("heading", name="谢谢你的提交，如果被采纳我们将通过邮件联系你❤️")).to_be_visible()
        expect(page.get_by_role("dialog")).to_contain_text("测试岛民")
        expect(page.get_by_role("dialog")).to_contain_text("islander@example.com")
        expect(page.get_by_role("dialog")).to_contain_text("希望加入更灵活的歌词行数")
        page.screenshot(path=str(SCREENSHOTS / "submission-ticket.png"), full_page=False)
        with page.expect_download() as download_info:
            page.get_by_role("button", name="下载 PNG 存根").click()
        download = download_info.value
        assert download.suggested_filename.endswith(".png")
        download.save_as(str(SCREENSHOTS / "submission-ticket-download.png"))
        page.get_by_role("button", name="完成").click()
        cookie_names = [cookie["name"] for cookie in page.context.cookies()]
        assert "lyric_island_contributor" in cookie_names
        page.get_by_role("tab", name="Bug 提交").click()
        expect(page.get_by_label("昵称")).to_have_value("测试岛民")
        expect(page.get_by_label("邮箱")).to_have_value("islander@example.com")

    assert not errors, f"{path} console errors: {errors}"


def test_admin_shell(page: Page) -> None:
    response = page.goto(BASE_URL + "/admin", wait_until="networkidle")
    assert response is not None and response.ok
    expect(page.get_by_role("heading", name="审阅用户提交")).to_be_visible()
    expect(page.get_by_label("后台密码")).to_be_visible()
    expect(page.get_by_role("button", name="进入后台")).to_be_visible()


def test_waterfall_breakpoint(page: Page, expected_columns: int) -> None:
    response = page.goto(BASE_URL + "/incentives", wait_until="networkidle")
    assert response is not None and response.ok
    visible_columns = page.locator(".acceptedWaterfallColumn").evaluate_all(
        "(columns) => columns.filter((column) => getComputedStyle(column).display !== 'none').length"
    )
    assert visible_columns == expected_columns
    assert page.evaluate("document.documentElement.scrollWidth - innerWidth") <= 1


def test_admin_dashboard(page: Page) -> None:
    saved_reviews: list[dict] = []

    def handle_submissions(route) -> None:
        if route.request.method == "PATCH":
            payload = route.request.post_data_json
            saved_reviews.append(payload)
            route.fulfill(
                status=200,
                content_type="application/json",
                body=json.dumps(
                    {
                        "submission": {
                            "id": "s1",
                            "kind": "feature",
                            "nickname": "小林",
                            "email": "lin@example.com",
                            "title": "自动切换歌词布局",
                            "body": "希望窗口变窄时自动切成单行。",
                            "attachments": [],
                            "like_count": 0,
                            "created_at": "2026-07-14T05:00:00Z",
                            "updated_at": "2026-07-18T10:00:00Z",
                            **payload,
                        }
                    },
                    ensure_ascii=False,
                ),
            )
            return
        route.fulfill(
            status=200,
            content_type="application/json",
            body=(
                '{"submissions":['
                '{"id":"s1","kind":"feature","nickname":"小林","email":"lin@example.com",'
                '"title":"自动切换歌词布局","body":"希望窗口变窄时自动切成单行。","attachments":[],"like_count":0,'
                '"status":"pending","reward_status":"not_eligible","developer_reply":null,"is_flagged":false,"is_public":false,'
                '"created_at":"2026-07-14T05:00:00Z","updated_at":"2026-07-14T05:00:00Z"},'
                '{"id":"s2","kind":"bug","nickname":"Sea","email":"sea@example.com",'
                '"title":"切换播放器后歌词停住","body":"从 Spotify 切到酷狗时偶尔出现。","attachments":[],"like_count":0,'
                '"status":"reviewing","reward_status":"pending","developer_reply":"等待复现","is_flagged":false,"is_public":false,'
                '"created_at":"2026-07-14T04:00:00Z","updated_at":"2026-07-14T04:00:00Z"}'
                ']} '
            ),
        )

    page.route(
        "**/api/incentives/admin/submissions",
        handle_submissions,
    )
    page.route(
        "**/api/incentives/admin/previews",
        lambda route: route.fulfill(
            status=200,
            content_type="application/json",
            body=(
                '{"previews":[{"id":"p1","version":"v2.1 Preview",'
                '"title_zh":"更安静的桌面交互","title_en":"Quieter desktop interactions",'
                '"body_zh":"继续打磨避让和收起体验。","body_en":"More polish.",'
                '"highlights_zh":[],"highlights_en":[],"target_date":"2026-09-01",'
                '"status":"published","created_at":"2026-07-14T00:00:00Z",'
                '"updated_at":"2026-07-14T00:00:00Z","published_at":"2026-07-14T00:00:00Z"}]}'
            ),
        ),
    )
    response = page.goto(BASE_URL + "/admin", wait_until="networkidle")
    assert response is not None and response.ok
    expect(page.get_by_role("heading", name="审阅队列")).to_be_visible()
    expect(page.locator(".reviewCard")).to_have_count(2)
    feature_card = page.locator(".reviewCard").filter(has_text="自动切换歌词布局")
    expect(feature_card).to_have_count(1)
    expect(feature_card.get_by_role("button", name="采纳后可展示", exact=True)).to_be_disabled()
    feature_card.get_by_role("button", name="已采纳", exact=True).click()
    feature_card.get_by_role("button", name="在前台展示", exact=True).click()
    feature_card.get_by_label("开发者回复").fill("下个版本会加入。")
    feature_card.get_by_role("button", name="保存审阅", exact=True).click()
    expect(feature_card.get_by_role("status")).to_have_text("✓ 已保存，公开页面刷新后会显示最新内容")
    assert len(saved_reviews) == 1
    assert saved_reviews[0]["status"] == "accepted"
    assert saved_reviews[0]["is_public"] is True
    assert saved_reviews[0]["developer_reply"] == "下个版本会加入。"
    feature_card.get_by_role("button", name="🚩 红旗标注", exact=True).click()
    expect(feature_card.get_by_role("status")).to_have_text("修改后请点击保存审阅")
    page.get_by_role("button", name="版本预告").click()
    expect(page.get_by_role("heading", name="发布版本预告")).to_be_visible()
    expect(page.locator(".previewEditor")).to_be_visible()
    expect(page.get_by_label("更新内容（中文）")).to_be_visible()
    expect(page.get_by_label("Update content (English)")).to_be_visible()
    tbd_button = page.get_by_role("button", name="上线时间待定")
    expect(tbd_button).to_be_visible()
    tbd_button.click()
    expect(tbd_button).to_have_attribute("aria-pressed", "true")
    expect(page.get_by_label("预计上线时间")).to_be_disabled()


def test_submission_validation(page: Page) -> None:
    response = page.request.post(
        BASE_URL + "/api/incentives/submissions",
        multipart={
            "kind": "feature",
            "nickname": "岛民",
            "email": "not-an-email",
            "title": "测试标题",
            "body": "这是足够长但邮箱无效的测试说明内容。",
        },
    )
    assert response.status == 400, "Public submissions must validate identity before storage"


def main() -> None:
    SCREENSHOTS.mkdir(parents=True, exist_ok=True)

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, channel="chrome")

        for name, width, height in VIEWPORTS:
            context = browser.new_context(
                viewport={"width": width, "height": height},
                reduced_motion="reduce" if name == "mobile" else "no-preference",
            )
            page = context.new_page()
            assert_page_health(page, "/", "zh-CN")
            if name in {"desktop", "mobile"}:
                test_interactions(page, "zh")
            if name == "desktop":
                test_smooth_section_snap(page)
                test_navigation_and_orbit(page)
                test_selective_text_reveal(page)
            if name == "mobile":
                assert page.locator("html").get_attribute("data-snap-scroll") is None, (
                    "Reduced-motion mobile contexts must keep native scrolling"
                )
                mobile_experience_image = page.locator(
                    "#experience .portraitImage"
                ).first
                mobile_transform_before_hover = mobile_experience_image.evaluate(
                    "(image) => getComputedStyle(image).transform"
                )
                mobile_experience_image.hover()
                page.wait_for_timeout(240)
                mobile_transform_after_hover = mobile_experience_image.evaluate(
                    "(image) => getComputedStyle(image).transform"
                )
                assert mobile_transform_after_hover == mobile_transform_before_hover, (
                    "Mobile experience images must not gain a hover zoom"
                )
                test_selective_text_reveal(page, reduced_motion=True)
            page.goto(BASE_URL + "/", wait_until="networkidle")
            page.evaluate("window.scrollTo(0, 0)")
            settle_reveals_for_screenshot(page)
            page.screenshot(
                path=str(SCREENSHOTS / f"zh-{name}.png"),
                full_page=True,
            )
            context.close()

        for name, width, height in [("desktop", 1440, 900), ("mobile", 390, 844)]:
            context = browser.new_context(viewport={"width": width, "height": height})
            page = context.new_page()
            assert_page_health(page, "/en", "en")
            if name == "desktop":
                test_interactions(page, "en")
            page.goto(BASE_URL + "/en", wait_until="networkidle")
            page.evaluate("window.scrollTo(0, 0)")
            settle_reveals_for_screenshot(page)
            page.screenshot(
                path=str(SCREENSHOTS / f"en-{name}.png"),
                full_page=True,
            )
            context.close()

        for path, lang, filename in [
            ("/updates", "zh-CN", "zh-updates.png"),
            ("/en/updates", "en", "en-updates.png"),
        ]:
            context = browser.new_context(viewport={"width": 1024, "height": 900})
            page = context.new_page()
            assert_page_health(page, path, lang)
            expect(page.locator(".releaseSection")).to_have_count(6)
            expect(page.locator(".boundariesSection")).to_have_count(0)
            expect(page.locator(".updatesDownloads .button")).to_have_count(1)
            expect(page.locator(".updatesDownloads a[href*='apps.microsoft.com']")).to_have_count(1)
            expect(page.locator(".updatesDownloads a[href*='github.com']")).to_have_count(0)
            assert page.locator("[data-text-reveal]").count() == 4
            expect(page.locator(".releaseSection h2[data-text-reveal]")).to_have_count(0)
            page.goto(BASE_URL + path, wait_until="networkidle")
            page.evaluate("window.scrollTo(0, 0)")
            settle_reveals_for_screenshot(page)
            page.screenshot(
                path=str(SCREENSHOTS / filename),
                full_page=True,
            )
            context.close()

        for path, lang, filename, mobile in [
            ("/incentives", "zh-CN", "zh-incentives.png", False),
            ("/en/incentives", "en", "en-incentives-mobile.png", True),
        ]:
            context = browser.new_context(
                viewport={"width": 390, "height": 844} if mobile else {"width": 1440, "height": 900},
                reduced_motion="reduce" if mobile else "no-preference",
            )
            page = context.new_page()
            test_incentive_page(page, path, lang, mobile)
            page.goto(BASE_URL + path, wait_until="networkidle")
            page.evaluate("window.scrollTo(0, 0)")
            settle_reveals_for_screenshot(page)
            page.screenshot(path=str(SCREENSHOTS / filename), full_page=True)
            context.close()

        for width, expected_columns in [(1024, 4), (768, 3)]:
            context = browser.new_context(
                viewport={"width": width, "height": 900},
                reduced_motion="no-preference",
            )
            page = context.new_page()
            test_waterfall_breakpoint(page, expected_columns)
            context.close()

        context = browser.new_context(viewport={"width": 1280, "height": 900})
        page = context.new_page()
        test_admin_shell(page)
        test_submission_validation(page)
        page.screenshot(path=str(SCREENSHOTS / "admin-login.png"), full_page=True)
        context.close()

        context = browser.new_context(viewport={"width": 1440, "height": 1000})
        page = context.new_page()
        test_admin_dashboard(page)
        page.screenshot(path=str(SCREENSHOTS / "admin-dashboard.png"), full_page=True)
        context.close()

        browser.close()

    print(
        "PASS: responsive home pages, bilingual updates, Microsoft Store links, "
        "incentive forms, shared identity cookies, admin login, interactions, focus, images, and overflow"
    )


if __name__ == "__main__":
    main()
