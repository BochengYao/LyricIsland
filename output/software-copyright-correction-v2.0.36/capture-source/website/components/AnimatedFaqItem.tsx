"use client";

import { useId, useState } from "react";

type Props = {
  question: string;
  answer: string;
};

export function AnimatedFaqItem({ question, answer }: Props) {
  const [open, setOpen] = useState(false);
  const identifier = useId().replace(/:/g, "");
  const buttonId = `faq-button-${identifier}`;
  const answerId = `faq-answer-${identifier}`;

  return (
    <div className={`faqItem${open ? " isOpen" : ""}`}>
      <h3 className="faqQuestion">
        <button
          id={buttonId}
          type="button"
          aria-expanded={open}
          aria-controls={answerId}
          onClick={() => setOpen((current) => !current)}
        >
          {question}
          <span aria-hidden="true">+</span>
        </button>
      </h3>
      <div
        id={answerId}
        className="faqAnswer"
        role="region"
        aria-labelledby={buttonId}
        aria-hidden={!open}
      >
        <div>
          <p>{answer}</p>
        </div>
      </div>
    </div>
  );
}
