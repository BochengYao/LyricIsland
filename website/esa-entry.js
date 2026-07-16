import vinextHandler from "./dist/server/index.js";

export default {
  fetch(request, env, context) {
    return vinextHandler(request, env, context);
  }
};
